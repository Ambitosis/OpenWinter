using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using Xunit;

namespace OpenWinter.Tests;

public class EquipmentActionPresentationTests
{
    sealed record Draw(uint Subject, uint? Right, uint? Left, string Animation);

    sealed class Output : IWorldOutput
    {
        public readonly List<Draw> Draws = [];
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        {
            if (kind != "equipment-draw") return;
            var pc = Assert.IsType<Creature>(subject);
            var payload = JsonSerializer.SerializeToElement(data);
            Draws.Add(new(pc.Id, pc.Equipment.GetValueOrDefault(EquipSlot.RightHand)?.Id,
                pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand)?.Id, payload.GetProperty("animation").GetString()!));
        }
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    static (World World, Creature Pc, Output Output) Load()
    {
        var world = new World(TestData.Module);
        var bic = Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "cleric.bic")));
        var pc = world.Creatures.FromTemplate(bic);
        world.Register(pc);
        world.Inventory.Load(pc);
        var output = new Output();
        world.Output = output;
        return (world, pc, output);
    }

    static void Request(World world, Creature pc, string kind, object data) =>
        Assert.True(ClientRequests.Handle(world, pc, kind, JsonSerializer.Serialize(data)));

    static void Step(World world, Creature pc)
    {
        ActionQueues.Step(world, pc, 1f / 30);
        Assert.Empty(pc.Actions);
    }

    static void FinalDraw(Creature pc, Output output, string animation = "drwright")
    {
        var draw = Assert.Single(output.Draws);
        Assert.Equal(pc.Id, draw.Subject);
        Assert.Equal(pc.Equipment.GetValueOrDefault(EquipSlot.RightHand)?.Id, draw.Right);
        Assert.Equal(pc.Equipment.GetValueOrDefault(EquipSlot.LeftHand)?.Id, draw.Left);
        Assert.Equal(animation, draw.Animation);
    }

    [DataFact]
    public void DefaultEquipExplicitPaperdollSlotAndScriptActionPublishTheSameFinalHands()
    {
        foreach (string route in new[] { "default", "explicit", "action" })
        {
            var (world, pc, output) = Load();
            var item = pc.Equipment[EquipSlot.RightHand];
            var left = pc.Equipment[EquipSlot.LeftHand];
            Assert.True(world.Inventory.Unequip(pc, item));
            output.Draws.Clear();
            if (route == "action") pc.Actions.Add(new EquipItemAction { ItemId = item.Id, Slot = 4 });
            else if (route == "explicit") Request(world, pc, "equip", new { item = item.Id, slot = 4 });
            else Request(world, pc, "equip", new { item = item.Id });
            Assert.Single(pc.Actions);
            Assert.Empty(output.Draws);
            Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
            Step(world, pc);
            Assert.Same(item, pc.Equipment[EquipSlot.RightHand]);
            Assert.Same(left, pc.Equipment[EquipSlot.LeftHand]);
            FinalDraw(pc, output);
        }
    }

    [DataFact]
    public void UnequipAndBackpackDragWithOrWithoutCellPublishTheSameFinalHands()
    {
        foreach (string route in new[] { "unequip", "move", "cell", "action" })
        {
            var (world, pc, output) = Load();
            var item = pc.Equipment[EquipSlot.RightHand];
            var left = pc.Equipment[EquipSlot.LeftHand];
            var cell = world.Inventory.FirstFit(pc, item)!.Value;
            if (route == "action") pc.Actions.Add(new UnequipItemAction { ItemId = item.Id });
            else if (route == "cell") Request(world, pc, "item-move", new { item = item.Id, x = cell.X, y = cell.Y });
            else Request(world, pc, route == "move" ? "item-move" : "unequip", new { item = item.Id });
            Assert.Single(pc.Actions);
            Assert.Same(item, pc.Equipment[EquipSlot.RightHand]);
            Assert.Empty(output.Draws);
            Step(world, pc);
            Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
            Assert.Same(left, pc.Equipment[EquipSlot.LeftHand]);
            Assert.Contains(item, pc.Inventory);
            if (route == "cell") Assert.Equal(cell, (item.InvX, item.InvY));
            FinalDraw(pc, output);
        }
    }

    [DataFact]
    public void PairRemovalAndRestorationEachPublishOneNotificationAfterBothHandsChange()
    {
        var (world, pc, output) = Load();
        var right = pc.Equipment[EquipSlot.RightHand];
        var left = pc.Equipment[EquipSlot.LeftHand];
        Request(world, pc, "equip-pair", new { item = right.Id, secondaryItem = left.Id });
        Step(world, pc);
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.LeftHand));
        FinalDraw(pc, output);
        output.Draws.Clear();
        Request(world, pc, "equip-pair", new { item = right.Id, secondaryItem = left.Id });
        Step(world, pc);
        Assert.Same(right, pc.Equipment[EquipSlot.RightHand]);
        Assert.Same(left, pc.Equipment[EquipSlot.LeftHand]);
        FinalDraw(pc, output);
    }

    [DataFact]
    public void LeftOnlyCommandsUseLeftDrawWhileCompoundCommandsRetainRightDraw()
    {
        var (world, pc, output) = Load();
        var right = pc.Equipment[EquipSlot.RightHand];
        var left = pc.Equipment[EquipSlot.LeftHand];
        Request(world, pc, "unequip", new { item = left.Id });
        Step(world, pc);
        FinalDraw(pc, output, "drwleft");
        output.Draws.Clear();
        Request(world, pc, "equip", new { item = left.Id, slot = 5 });
        Step(world, pc);
        FinalDraw(pc, output, "drwleft");
        Assert.True(world.Inventory.Unequip(pc, left));
        output.Draws.Clear();
        Request(world, pc, "equip-pair", new { item = right.Id, secondaryItem = left.Id });
        Step(world, pc);
        FinalDraw(pc, output);
    }

    [DataFact]
    public void RejectedNoOpAndNonHandChangesDoNotPublishDraw()
    {
        var (world, pc, output) = Load();
        var right = pc.Equipment[EquipSlot.RightHand];
        var left = pc.Equipment[EquipSlot.LeftHand];
        Request(world, pc, "equip", new { item = right.Id, slot = 4 }); // Already equipped.
        Step(world, pc);
        Request(world, pc, "equip", new { item = right.Id, slot = 0 }); // Weapon cannot occupy head.
        Step(world, pc);
        Request(world, pc, "equip-pair", new { item = right.Id, secondaryItem = right.Id });
        Assert.Empty(pc.Actions);
        var armor = pc.Equipment[EquipSlot.Bit(1)];
        Request(world, pc, "unequip", new { item = armor.Id });
        Step(world, pc);
        Assert.Contains(armor, pc.Inventory);
        Request(world, pc, "unequip", new { item = armor.Id }); // Already in backpack.
        Step(world, pc);
        Request(world, pc, "equip", new { item = armor.Id, slot = 1 });
        Step(world, pc);
        Assert.Same(armor, pc.Equipment[EquipSlot.Bit(1)]);
        Assert.Same(right, pc.Equipment[EquipSlot.RightHand]);
        Assert.Same(left, pc.Equipment[EquipSlot.LeftHand]);
        Assert.Empty(output.Draws);
    }

    [DataFact]
    public void QueuedBackpackCellIsRecheckedAndOccupiedDestinationLeavesWeaponEquipped()
    {
        var (world, pc, output) = Load();
        var item = pc.Equipment[EquipSlot.RightHand];
        var blocker = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "cell_blocker")!;
        var cell = world.Inventory.FirstFit(pc, item)!.Value;
        Request(world, pc, "item-move", new { item = item.Id, x = cell.X, y = cell.Y });
        Assert.Single(pc.Actions);
        Assert.True(world.Inventory.MoveToCell(blocker, pc, cell.X, cell.Y));
        Assert.False(world.Inventory.FitsAt(pc, item, cell.X, cell.Y));
        output.Draws.Clear();
        Step(world, pc);
        Assert.Same(item, pc.Equipment[EquipSlot.RightHand]);
        Assert.DoesNotContain(item, pc.Inventory);
        Assert.Equal(cell, (blocker.InvX, blocker.InvY));
        Assert.Empty(output.Draws);
        // Rejection did not discard the item or prevent a later valid command.
        var free = world.Inventory.FirstFit(pc, item)!.Value;
        Request(world, pc, "item-move", new { item = item.Id, x = free.X, y = free.Y });
        Step(world, pc);
        Assert.Equal(free, (item.InvX, item.InvY));
        FinalDraw(pc, output);
    }

    [DataFact]
    public void QueuedEquipRechecksOwnershipAndDoesNotAnimateARejectedTransfer()
    {
        var (world, pc, output) = Load();
        var item = pc.Equipment[EquipSlot.RightHand];
        Assert.True(world.Inventory.Unequip(pc, item));
        Request(world, pc, "equip", new { item = item.Id });
        var other = world.Creatures.FromTemplate(pc.Template);
        world.Register(other);
        Assert.NotNull(world.Inventory.MoveTo(item, other));
        output.Draws.Clear();
        Step(world, pc);
        Assert.Same(other, world.Inventory.Owner(item));
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.Empty(output.Draws);
    }

    [DataFact]
    public void EveryEquipmentRequestRejectsNoncommandableInputAndYieldsAttackWithoutLosingPendingTasks()
    {
        foreach (string route in new[] { "equip", "explicit", "unequip", "move", "cell", "pair" })
        {
            var (world, pc, output) = Load();
            var right = pc.Equipment[EquipSlot.RightHand];
            var left = pc.Equipment[EquipSlot.LeftHand];
            if (route is "equip" or "explicit") Assert.True(world.Inventory.Unequip(pc, right));
            var cell = world.Inventory.FirstFit(pc, right)!.Value;
            void Submit()
            {
                switch (route)
                {
                    case "explicit": Request(world, pc, "equip", new { item = right.Id, slot = 4 }); break;
                    case "move": Request(world, pc, "item-move", new { item = right.Id }); break;
                    case "cell": Request(world, pc, "item-move", new { item = right.Id, x = cell.X, y = cell.Y }); break;
                    case "pair": Request(world, pc, "equip-pair", new { item = right.Id, secondaryItem = left.Id }); break;
                    default: Request(world, pc, route, new { item = right.Id }); break;
                }
            }
            var attack = new OpenWinter.Game.Combat.AttackAction { Target = pc.Id };
            var pending = new WaitAction { Remaining = 1 };
            pc.Actions.Add(attack);
            pc.Actions.Add(pending);
            pc.Commandable = false;
            Submit();
            Assert.Equal(2, pc.Actions.Count);
            Assert.Same(attack, pc.Actions[0]);
            Assert.Same(pending, pc.Actions[1]);
            pc.Commandable = true;
            Submit();
            Assert.Equal(2, pc.Actions.Count);
            Assert.Same(pending, pc.Actions[0]);
            Assert.IsAssignableFrom<EquipmentAction>(pc.Actions[1]);
            ActionQueues.Step(world, pc, .1f);
            Assert.Equal(2, pc.Actions.Count);
            Assert.Empty(output.Draws);
            world.Step(1f); // WaitAction follows authoritative game time, not its dt argument.
            ActionQueues.Step(world, pc, 0);
            Assert.Empty(pc.Actions);
            FinalDraw(pc, output);
        }
    }
}
