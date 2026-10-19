using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // Chained put-downs, part of --capture-drop-freeze. The original stays bent through queued put-downs, 1.5 s each,
    // and stands once after the last; keys cancel the rest at once; a put-down issued after the previous one landed
    // has her stand and bend again (visual scene dropchain). The animator
    // is sampled every frame: "bent" is the action layer playing getlow or getlowlp with time left.
    async Task CaptureDropChain(string dir, Action<string, bool> check, Func<Anim.CreatureAnimator?> animator, Func<Task> reset)
    {
        var world = _server!.World;
        var pc = world.Get<Creature>(_session.ControlledId)!;
        bool Bent() => animator()?.CaptureAction()?.Clip is "getlow" or "getlowlp";
        // The skeleton's root height shows the pose itself: a frame of the standing pose inside the bend is a flicker.
        float Root()
        {
            var skeleton = ((Views.CreatureView)_session.Controlled!).Skeleton;
            int bone = skeleton?.FindBone("rootdummy") ?? -1;
            return bone < 0 ? float.NaN : skeleton!.GetBonePosePosition(bone).Z;
        }
        float standing = Root();
        int flickers = 0;
        Item Pack() => world.Inventory.CreateOn("nw_wswdg001", pc, 1, "CAPTURE_PACK")!; // daggers do not stack

        // Requests a put-down of each item at its second (NaN: 0.25 s after the previous one landed), presses W at
        // keyAt for 0.4 s if given, and samples every frame until the queue is empty and the bend has ended.
        async Task<(List<(double T, bool Bent)> Samples, double[] Landed)> Run(string name, double[] at, double keyAt = -1,
            string[]? shots = null, bool stall = false)
        {
            var items = at.Select(_ => Pack()).ToArray();
            await Wait(.6);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var samples = new List<(double T, bool Bent)>();
            var landed = at.Select(_ => double.NaN).ToArray();
            var sent = new bool[at.Length];
            int key = keyAt < 0 ? 2 : 0, shot = 0;
            double quietSince = double.NaN;
            var keyStart = pc.Position;
            var roots = new List<float>();
            bool low = false, diagnosed = false;
            float lowest = float.MaxValue;
            while (clock.Elapsed.TotalSeconds < 9)
            {
                double t = clock.Elapsed.TotalSeconds;
                for (int i = 0; i < at.Length; i++)
                {
                    double due = double.IsNaN(at[i]) ? i > 0 ? landed[i - 1] + .25 : 0 : at[i];
                    if (!sent[i] && t >= due) { sent[i] = true; _session.Request("drop", new { item = items[i].Id }); }
                }
                if (key == 0 && t >= keyAt)
                {
                    key = 1;
                    keyStart = pc.Position;
                    _session.Send(new MoveInputMessage { Direction = new System.Numerics.Vector3(0, 1, 0) }.Write());
                }
                else if (key == 1 && t >= keyAt + .4)
                {
                    key = 2;
                    GD.Print($"[drop-chain] {name}: W held from {keyStart} to {pc.Position}, speed {animator()?.GroundSpeed:0.00}");
                    _session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write());
                }
                for (int i = 0; i < at.Length; i++)
                    if (double.IsNaN(landed[i]) && items[i].Area != null)
                    {
                        landed[i] = t;
                        // Just after a landing is where a stand would begin; not awaited, so sampling goes on.
                        if (shots != null && shot < shots.Length) _ = Shot(dir, shots[shot++]);
                    }
                if (!diagnosed && t > .5 && samples.All(x => !x.Bent) && pc.Actions.Count == 0)
                {
                    diagnosed = true;
                    GD.Print($"[drop-chain] {name}: nothing started: commandable {pc.Commandable}, area {pc.Area?.Tag}, "
                        + $"owner {world.Inventory.Owner(items[0])?.Id} pc {pc.Id} controlled {_session.ControlledId}, droppable {items[0].Droppable}");
                }
                bool bent = Bent();
                samples.Add((t, bent));
                // Uneven, slow frames around the intro's end (a screenshot saved every frame stalls the next).
                if (stall && t > .3 && t < .8) _ = Shot(dir, $"stall_{samples.Count:000}");
                float root = Root();
                roots.Add(root);
                // Once low and still bent, the root must not jump back up towards standing height.
                if (bent && low && root > lowest + .2f) { flickers++; GD.Print($"[drop-chain] {name}: root up to {root:0.00} at {t:0.00} s while bent"); }
                if (bent && root < standing - .3f) low = true;
                lowest = bent ? Math.Min(lowest, root) : float.MaxValue;
                if (!bent) low = false;
                if (sent.Any(x => !x) || pc.Actions.Count > 0 || bent || key < 2) quietSince = double.NaN;
                else if (double.IsNaN(quietSince)) quietSince = t;
                else if (t - quietSince > .8) break;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            GD.Print($"[drop-chain] {name}: landed at {string.Join(", ", landed.Select(x => x.ToString("0.00")))} s; "
                + $"bent {FirstBent(samples):0.00}-{LastBent(samples):0.00} s; stands {Stands(samples)}; {samples.Count} samples; "
                + $"root {roots.Min():0.00}-{roots.Max():0.00} (standing {standing:0.00})");
            for (int i = 0; i < items.Length; i++)
                if (items[i].Area == null) GD.Print($"[drop-chain] {name}: item {i} still possessed: {items[i].Possessor == pc.Id}");
            return (samples, landed);
        }
        static int Stands(List<(double T, bool Bent)> s) => s.Zip(s.Skip(1)).Count(p => p.First.Bent && !p.Second.Bent);
        static double FirstBent(List<(double T, bool Bent)> s) => s.Where(x => x.Bent).Select(x => x.T).DefaultIfEmpty(double.NaN).First();
        static double LastBent(List<(double T, bool Bent)> s) => s.Where(x => x.Bent).Select(x => x.T).DefaultIfEmpty(double.NaN).Last();
        static bool BentBetween(List<(double T, bool Bent)> s, double from, double to) =>
            s.Where(x => x.T >= from && x.T <= to).All(x => x.Bent);

        // The client times the bend from the notice and the server lands the item on its own tick, so the stand may
        // begin a tick or two before the landing; the original's began within a frame (0.1 s) of it.
        const double Tick = .1;

        // Three put-downs requested together: each lands 1.5 s after the last, she stays bent throughout and stands once.
        var (samples, landed) = await Run("three put-downs", new[] { 0.0, 0, 0 },
            shots: new[] { "c_chain_after_first", "c_chain_after_second", "c_chain_after_third" });
        check("chain: all three land", landed.All(double.IsFinite));
        check("chain: 1.5 s apart", landed.Zip(landed.Skip(1)).All(p => p.Second - p.First is > 1.3 and < 1.75));
        check("chain: bent until the last lands", BentBetween(samples, FirstBent(samples), landed[^1] - Tick));
        check("chain: stands once, after the last", Stands(samples) == 1 && Math.Abs(LastBent(samples) - landed[^1]) < .4);
        await reset();

        // W pressed during the second put-down ends the bend at once; the second and third stay in the pack.
        (samples, landed) = await Run("three put-downs, W at 2.2 s", new[] { 0.0, 0, 0 }, keyAt: 2.2);
        check("chain move: the first lands, the others stay", double.IsFinite(landed[0]) && double.IsNaN(landed[1]) && double.IsNaN(landed[2]));
        check("chain move: bent until the move", BentBetween(samples, FirstBent(samples), 2.2));
        check("chain move: the bend ends at once", Stands(samples) == 1 && LastBent(samples) < 2.2 + .35);
        await reset();

        // A put-down requested while one bends joins it: one stand, after the second lands.
        (samples, landed) = await Run("second put-down at 0.7 s", new[] { 0.0, .7 });
        check("late chain: bent until the second lands, one stand", landed.All(double.IsFinite)
            && BentBetween(samples, FirstBent(samples), landed[1] - Tick) && Stands(samples) == 1);
        await reset();

        // One requested 0.25 s after the previous landed is not a chain: she stands and bends again, like the original.
        (samples, landed) = await Run("second put-down 0.25 s after the first landed", new[] { 0.0, double.NaN });
        check("gap: stands between and after", landed.All(double.IsFinite) && Stands(samples) == 2);
        await reset();

        // Slow, uneven frames at the switch from the intro (getlow) to the loop must not wrap the intro to its
        // standing first frame (CreatureAnimator.TryCastSequence).
        (samples, landed) = await Run("one put-down, stalled frames", new[] { 0.0 }, stall: true);
        foreach (var file in System.IO.Directory.GetFiles(dir, "stall_*.png")) System.IO.File.Delete(file);
        check("stalled frames: the put-down lands", double.IsFinite(landed[0]));
        await reset();
        check("no frame of the standing pose inside a bend", flickers == 0);
    }
}
