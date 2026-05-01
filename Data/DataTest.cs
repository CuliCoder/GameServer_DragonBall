public struct SkillInfo
{
    public int SkillId { get; set; }
    public string SkillName { get; set;}
    public float percentage { get; set; }
    public DamegeType DamegeType { get; set; }
    public float Range { get; set; }
    public float Cooldown { get; set; }
    public float cost { get; set; }
}
public enum DamegeType
{
    Hp,
    Ki,
    Sd
}

public static class DataTest
{
    public static List<SkillInfo> SkillInfos = new List<SkillInfo>
    {
        new SkillInfo { SkillId = 1, SkillName = "Boom", percentage = 50f, DamegeType = DamegeType.Hp, Range = 100f, Cooldown = 3.0f ,cost = 100f},
    };
    public static SkillInfo GetSkillInfo(int skillId)
    {
        return SkillInfos.FirstOrDefault(s => s.SkillId == skillId);
    }
}   