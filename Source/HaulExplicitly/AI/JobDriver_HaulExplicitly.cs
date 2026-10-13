using HaulExplicitly.Extension;
using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

/// <summary>
/// 显式搬运工作：把玩家指定的物品搬运到玩家指定的目的地。
/// 负责占用目标、前往并拾取物品、搬运到目的地放下，并把搬运进展写回搬运记录。
/// </summary>
public class JobDriver_HaulExplicitly : JobDriver
{
    private InventoryRecord_DesignatorHaulExplicitly? _record;

    /// <summary>
    /// 本次工作对应的搬运记录（同类物品的数量与进展）；尚未初始化时会先调用 <see cref="Init"/>。
    /// </summary>
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
    }

    private Data_DesignatorHaulExplicitly? _data;

    /// <summary>
    /// 本次工作对应的搬运数据；尚未初始化时会先调用 <see cref="Init"/>。
    /// </summary>
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
    }

    /// <summary>
    /// 所搬运数据在 <see cref="JobManager_DesignatorHaulExplicitly"/> 中的 ID，
    /// 借助 <c>job.targetQueueA[0]</c> 格子的 x 分量随存档一起保存。
    /// </summary>
    public int DataIndex => job.targetQueueA[0].Cell.x;

    /// <summary>
    /// 目的地当前还能容纳的物品数量，借助 <c>job.targetQueueA[0]</c> 格子的 y 分量随存档一起保存。
    /// </summary>
    public int DestinationSpaceAvailable
    {
        get { return job.targetQueueA[0].Cell.y; }
        set
        {
            var c = job.targetQueueA[0].Cell;
            job.targetQueueA[0] = new LocalTargetInfo(new IntVec3(c.x, value, c.z));
        }
    }

    /// <summary>
    /// 依据工作的目标物品初始化 <c>_data</c> 与 <c>_record</c>：
    /// 从物品所在地图取出对应的搬运数据，并找到（或新建）能容纳该物品的搬运记录。
    /// </summary>
    public void Init() // 当获取 _data 或 _record 为 null 时，初始化
    {
        Thing targetItem = job.targetA.Thing;
        if (targetItem.MapHeld == null)
        {
            _data = new Data_DesignatorHaulExplicitly();
        }
        else
        {
            _data = GameComponent_HaulExplicitly.GetManager(targetItem.MapHeld).datas[DataIndex];
        }

        _record = _data.inventory.FirstOrDefault(r => r.CanAdd(targetItem));
        if (_record != null) return;
        _record = new InventoryRecord_DesignatorHaulExplicitly(targetItem, _data);
        _data.inventory.Add(_record);
    }


    /// <summary>
    /// 开始执行前占用目标物品与全部目的地格子，防止其他殖民者抢占，
    /// 并把目标物品标记为「不要自动搬走」。
    /// </summary>
    /// <param name="errorOnFailed">占用失败时是否记录错误。</param>
    /// <returns>全部目标都成功占用返回 <c>true</c>。</returns>
    public override bool TryMakePreToilReservations(bool errorOnFailed) // 尝试占用目标，防止他人再与其互动
    {
        Thing thing = job.GetTarget(TargetIndex.A).Thing;
        thing.SetDontMoved(true);

        List<LocalTargetInfo> targets = [TargetA, TargetB];
        targets.AddRange(job.targetQueueB);
        return targets.All(t => pawn.Reserve(t, job, 1, -1, null, errorOnFailed));
    }

    /// <summary>
    /// 构建工作的执行步骤：前往物品 → 拾取 → 搬运到目的地 → 在目的地放下。
    /// 途中若物品被销毁、目的地起火或物品被禁用，则会中止工作；
    /// 若目的地上已有的物品无法与手上的物品堆叠，也会中止。
    /// </summary>
    /// <returns>本工作的 Toil 序列。</returns>
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.B);
        this.FailOnForbidden(TargetIndex.A);

        Toil gotoThing = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
        gotoThing.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
        gotoThing.FailOn(toil =>
        {
            Job actorCurJob = toil.actor.CurJob;

            IntVec3 dest = actorCurJob.GetTarget(TargetIndex.B).Cell;
            List<Thing>? itemsInCell = Data_DesignatorHaulExplicitly.GetItemsIfValidItemSpot(toil.actor.Map, dest);
            if (itemsInCell == null) return true; // 返回 true 来终止toil
            switch (itemsInCell.Count)
            {
                case 0:
                case 1 when actorCurJob.GetTarget(TargetIndex.A).Thing.CanStackWith(itemsInCell.First()):
                    return false;
                default:
                    return true;
            }
        });
        yield return gotoThing;
        yield return Toils_HaulExplicitly.PickUpThing(TargetIndex.A);
        Toil carryToDest = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
        yield return carryToDest;
        yield return Toils_HaulExplicitly.PlaceHauledThingAtDest(TargetIndex.B, carryToDest);
    }
}