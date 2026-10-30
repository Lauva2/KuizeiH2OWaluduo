using UnityEngine;
using RaftGame.Needs;
using RaftGame.Building;
namespace RaftGame.UI
{
    public sealed class ComfortPanelModel : UIModel
    {
        public sealed class Row { public string Name, Contribution, Next; }
        public readonly Row[] Rows = { new Row(), new Row(), new Row() };
        public string Score, Hint;
        public void Refresh(ComfortManager comfort)
        {
            Score = $"舒适度 {comfort.Score:0.#} / 100   ·   休息恢复 +{(comfort.RestMultiplier - 1) * 100:0.#}%";
            Hint = $"营地已布置 {comfort.Count} 件装饰 · 同类重复摆放的贡献逐次递减\n多搭配几种装饰，让木筏更像家。";
            for (int index = 0; index < Rows.Length; index++)
            {
                var kind = ComfortManager.Kinds[index]; var row = Rows[index]; row.Name = FacilityRules.Name(kind);
                row.Contribution = $"已建 {comfort.CountOf(kind)} 件 · 同类贡献 {comfort.Contribution(kind):0.#} 分";
                float next = Mathf.Min(comfort.NextContribution(kind), 100 - comfort.Score);
                row.Next = comfort.Score >= 100 ? "已达到营地舒适上限" : $"再布置一个，营地舒适度 +{next:0.#}";
            }
            NotifyChanged();
        }
    }
}
