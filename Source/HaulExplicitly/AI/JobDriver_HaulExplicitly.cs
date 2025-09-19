using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

public class JobDriver_HaulExplicitly : JobDriver
{
    private InventoryRecord_DesignatorHaulExplicitly? _record;

    public InventoryRecord_DesignatorHaulExplicitly record
    {
        get
        {
            if (_record == null) // 处理 _record 的可空问题
            {
                Init();
            }

            return _record ?? throw new InvalidOperationException();
        }
        private set => _record = value;
    }

    private Data_DesignatorHaulExplicitly? _data;

    public Data_DesignatorHaulExplicitly Data
    {
        get
        {
            if (_data == null) // 处理 _data 的可空问题
            {
                Init();
            }

            return _data ?? throw new InvalidOperationException();
        }
        private set => _data = value;
    }

    public int DataIndex => job.targetQueueA[0].Cell.x;

    public int DestinationSpaceAvailable
    {
        get { return job.targetQueueA[0].Cell.y; }
        set
        {
            var c = job.targetQueueA[0].Cell;
            job.targetQueueA[0] = new LocalTargetInfo(new IntVec3(c.x, value, c.z));
        }
    }

    public void Init() // 初始化
    {
        Thing targetItem = job.targetA.Thing;
        _data = GameComponent_HaulExplicitly.GetManager(targetItem.MapHeld).datas[DataIndex];
        _record = _data.GetRecordWhichWithItem(targetItem);
    }

    // 尝试占用目标，防止他人再与其互动
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        List<LocalTargetInfo> targets = [TargetA, TargetB];
        targets.AddRange(job.targetQueueB); // 🤔
        return targets.All(t => pawn.Reserve(t, job, 1, -1, null, errorOnFailed));
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.B);
        this.FailOnForbidden(TargetIndex.A);

        Toil gotoThing = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
        gotoThing.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
        gotoThing.FailOn(toil =>
        {
            Job job = toil.actor.CurJob;
            Thing thing = job.GetTarget(TargetIndex.A).Thing;
            IntVec3 dest = job.GetTarget(TargetIndex.B).Cell;
            List<Thing> itemsInCell = Data_DesignatorHaulExplicitly.GetItemsIfValidItemSpot(toil.actor.Map, dest);
            if (itemsInCell == null) return true;
            switch (itemsInCell.Count)
            {
                case 0:
                case 1 when thing.CanStackWith(itemsInCell.First()):
                    return false;
                default:
                    return true;
            }
        });
        yield return gotoThing;
        yield return Toils_HaulExplicitly.PickUpThing(TargetIndex.A, gotoThing);
        Toil carryToDest = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
        yield return carryToDest;
        yield return Toils_HaulExplicitly.PlaceHauledThingAtDest(TargetIndex.B, carryToDest);
    }
}