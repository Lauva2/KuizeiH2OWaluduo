using UnityEngine;
using RaftGame.Crew;
namespace RaftGame.UI
{
    public sealed class CrewPanelModel : UIModel
    {
        public sealed class Row { public string Name, Needs, Energy, Activity, Location; public bool Present; public WorkPreference Preference; }
        public readonly Row[] Rows = { new Row(), new Row() };
        public string Heading, Notice;
        public void Refresh(CrewManager crew, RescueManager rescue)
        {
            Heading = $"营地船员  {crew.Count} / {CrewManager.Capacity}";
            Notice = (rescue != null ? rescue.Message : "漂流途中寻找新伙伴") + "\n生活需求优先，偏好工作不足时会自动补位。";
            for (int index = 0; index < Rows.Length; index++)
            {
                var row = Rows[index]; row.Present = index < crew.Count;
                if (!row.Present) { row.Name = "等待新伙伴"; row.Needs = "海面出现救生筏时，点击标记救援"; row.Energy = "加入后也需要床位休息"; row.Activity = "营地最多容纳两名船员"; row.Location = "加入后会增加食物和淡水消耗"; continue; }
                var member = crew.Members[index]; row.Name = member.DisplayName; row.Preference = member.Preference;
                row.Needs = member.Needs != null ? $"饱腹 {Mathf.FloorToInt(member.Needs.Satiety)}% · 水分 {Mathf.FloorToInt(member.Needs.Hydration)}% · 效率 {Mathf.RoundToInt(member.Needs.Efficiency * 100)}%" : "生活需求 —";
                row.Energy = member.Needs != null ? $"精力 {Mathf.FloorToInt(member.Needs.Energy)}%" + (crew.Rest != null && crew.Rest.NeedsRest(member.Needs) && member.Activity != CrewActivity.Resting ? " · 寻找可用床位" : " · 床上休息可恢复") : "精力 —";
                if (member.Needs != null && member.Activity == CrewActivity.Resting && crew.Rest != null) row.Energy = $"精力 {Mathf.FloorToInt(member.Needs.Energy)}% · 恢复速度 +{(crew.Rest.RecoveryMultiplier - 1) * 100:0.#}%";
                row.Activity = member.StatusText.Replace("船员：", ""); row.Location = $"甲板 ({member.CurrentCell.x}, {member.CurrentCell.y}) · 工作进度 {Mathf.FloorToInt(member.WorkProgress * 100)}%";
            }
            NotifyChanged();
        }
    }
}
