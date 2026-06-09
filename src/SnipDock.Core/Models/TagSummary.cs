namespace SnipDock.Core.Models
{
    public class TagSummary
    {
        public string TagName { get; set; } = string.Empty;
        public int Count { get; set; }

        public string DisplayText => $"{TagName} ({Count})";
    }
}
