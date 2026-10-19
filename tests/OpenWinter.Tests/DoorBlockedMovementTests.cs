using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

public sealed class DoorBlockedMovementTests
{
    sealed class Fixture
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Creature Actor;
        public readonly Door Door;
        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            Area = new Area { Width = 4, Height = 4 }; World.Register(Area); World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0,0,0), new(40,0,0), new(40,40,0), 3, true);
            Area.Walkmesh.Add(new(0,0,0), new(40,40,0), new(0,40,0), 3, true);
            Door = new Door { Tag = "DOOR", Position = new(10,10,0) }; World.AddToArea(Door, Area, Door.Position, 0);
            Door.WalkmeshOwner = (int)Door.Id;
            Area.Walkmesh.SetClosedDoorOwner(Door.WalkmeshOwner);
            Rectangle(8,9.85f,12,10.15f,Door.WalkmeshOwner);
            Actor = new Creature { Tag = "ACTOR", WalkRate = 1.75f, RunRate = 3.5f };
            World.AddToArea(Actor, Area, new(10,5,0), 0, exact: true);
            Script("blocked", "void main(){SetLocalInt(OBJECT_SELF,\"count\",GetLocalInt(OBJECT_SELF,\"count\")+1);SetLocalObject(OBJECT_SELF,\"door\",GetBlockingDoor());SetLocalInt(OBJECT_SELF,\"action\",GetCurrentAction(OBJECT_SELF));if(GetLocalInt(OBJECT_SELF,\"recover\")){ActionOpenDoor(GetBlockingDoor());ActionMoveToLocation(Location(GetArea(OBJECT_SELF),Vector(10.0,15.0,0.0),0.0),FALSE);}}");
            Actor.Scripts["ScriptOnBlocked"] = "blocked";
            Script("move", "void main(){ActionMoveToLocation(Location(GetArea(OBJECT_SELF),Vector(10.0,15.0,0.0),0.0),FALSE);ActionDoCommand(SetLocalInt(OBJECT_SELF,\"continued\",1));ActionWait(0.1);}");
        }
        public void Rectangle(float x0, float y0, float x1, float y1, int owner)
        {
            float z = owner == 0 ? 0 : 1;
            Area.Walkmesh.Add(new(x0,y0,z),new(x1,y0,z),new(x1,y1,z),7,false,owner);
            Area.Walkmesh.Add(new(x0,y0,z),new(x1,y1,z),new(x0,y1,z),7,false,owner);
        }
        void Script(string name, string source)
        {
            var bytes = NssCompiler.Compile(source,name,World.Scripts!.Definitions,_=>null,out var error);
            Assert.True(bytes != null,error); World.Scripts.Register(name,new NcsProgram(name,bytes!));
        }
        public void Run(float seconds = 12)
        {
            World.Scripts!.Run("move",Actor.Id);
            for (int i=0; i<seconds*30; i++) World.Step(1f/30);
        }
    }

    [DataFact]
    public void ClosedDoorStopsOnceAndRemovesMoveBeforeItsHook()
    {
        var f = new Fixture(); f.Run();
        Assert.Equal(1,f.Actor.Locals.Ints["count"]);
        Assert.Equal(f.Door.Id,f.Actor.Locals.Objects["door"]);
        Assert.Equal(65535,f.Actor.Locals.Ints["action"]);
        Assert.Equal(1,f.Actor.Locals.Ints["continued"]);
        Assert.InRange(f.Actor.Position.Y,9.7f,9.85f);
        Assert.False(f.Door.IsOpen);
    }

    [DataFact]
    public void BlockedHookCanOpenTheDoorAndQueueAnotherMove()
    {
        var f = new Fixture(); f.Actor.Locals.Ints["recover"]=1; f.Run();
        Assert.Equal(1,f.Actor.Locals.Ints["count"]);
        Assert.True(f.Door.IsOpen);
        Assert.InRange(f.Actor.Position.Y,14.99f,15.01f);
    }

    [DataFact]
    public void OpenDoorsAndWallsDoNotRaiseDoorBlocked()
    {
        var f = new Fixture();
        Assert.True(DoorService.Open(f.World,f.Door,f.Door));
        f.Run();
        Assert.False(f.Actor.Locals.Ints.ContainsKey("count"));
        Assert.InRange(f.Actor.Position.Y,14.99f,15.01f);
        var wall = new Fixture(); wall.Area.Walkmesh.SetOwnerEnabled(wall.Door.WalkmeshOwner,false);
        wall.Rectangle(0,9.7f,40,10.3f,0);
        wall.Run();
        Assert.False(wall.Actor.Locals.Ints.ContainsKey("count"));
        Assert.True(wall.Actor.Position.Y<10);
    }

    [DataFact]
    public void ALongStepCannotTunnelThroughTheClosedSlab()
    {
        var f = new Fixture();
        f.World.Jump(f.Actor,new Location(f.Area.Id,new(10,9,0),0));
        f.Actor.MoveGoal=new(10,15,0); f.Actor.MoveDirection=Vector3.UnitY; f.Actor.MovePassClosedDoors=true;
        Movement.Step(f.World,f.Actor,1f);
        Assert.Equal(f.Door.Id,f.Actor.MoveBlockedBy);
        Assert.Equal(9f,f.Actor.Position.Y);
    }

    /// <summary>A wall with a locked door and an open gap further along: only scripted moves walk into the door.</summary>
    static Fixture WallWithGap()
    {
        var f = new Fixture(); f.Door.Locked = true;
        f.Rectangle(0,9.85f,8,10.15f,0); f.Rectangle(12,9.85f,20,10.15f,0); f.Rectangle(23,9.85f,40,10.15f,0);
        return f;
    }

    [DataFact]
    public void OrdinaryMovesGoRoundAClosedDoorThroughTheGap()
    {
        var f = WallWithGap();
        f.Actor.Actions.Add(new MoveAction { Target = new(10,15,0), Run = true, Range = 0.3f, AllowBackward = true });
        for (int i=0; i<20*30; i++) f.World.Step(1f/30);
        Assert.False(f.Actor.Locals.Ints.ContainsKey("count"));
        Assert.Equal(GameObject.InvalidId,f.Actor.LastBlockingDoor);
        Assert.InRange(f.Actor.Position.Y,14.99f,15.01f);
        Assert.InRange(f.Actor.Position.X,9.99f,10.01f);
        Assert.False(f.Door.IsOpen);
    }

    [DataFact]
    public void ScriptedMovesStillStopAgainstTheDoorBesideAGap()
    {
        var f = WallWithGap(); f.Run();
        Assert.Equal(1,f.Actor.Locals.Ints["count"]);
        Assert.Equal(f.Door.Id,f.Actor.Locals.Objects["door"]);
        Assert.InRange(f.Actor.Position.Y,9.7f,9.85f);
    }

    [DataFact]
    public void OnlyMovementPlannerTraversesClosedSlabsAndKeepsOpenLeavesBlocking()
    {
        var f = new Fixture(); var start=new Vector3(10,5,0); var goal=new Vector3(10,15,0);
        Assert.Single(f.Area.MovementPathfinder.FindPath(start,goal)!);
        Assert.True(f.Area.Pathfinder.FindPath(start,goal)!.Count>1);
        // A door opening invalidates both planners; its untagged open leaf stays a wall.
        f.Area.Walkmesh.SetOwnerEnabled(f.Door.WalkmeshOwner,false);
        f.Rectangle(9.5f,9.8f,10.5f,12,f.Door.LeafOwner(1));
        Assert.True(f.Area.MovementPathfinder.FindPath(start,goal)!.Count>1);
        Assert.Null(f.Area.Walkmesh.GroundHeight(10,10,0));
    }
}
