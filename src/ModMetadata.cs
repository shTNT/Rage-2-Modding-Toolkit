using System;

namespace Rage2Toolkit
{
    public class ModMetadata
    {
        public int Schema { get; set; } = 1;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Game { get; set; } = "RAGE2";
        public string[] GameVersions { get; set; }
        public string CreatedWith { get; set; }
        public string CreatedAt { get; set; }
        public string License { get; set; }
        public string Homepage { get; set; }
        public string[] Dependencies { get; set; }
        public string[] Conflicts { get; set; }

        public static ModMetadata Default(string fallbackName)
        {
            return new ModMetadata
            {
                Schema = 1,
                Id = Slugify(fallbackName ?? "untitled"),
                Name = fallbackName ?? "Untitled Mod",
                Version = "0.0.0",
                Author = "Unknown",
                Description = "",
                Game = "RAGE2",
                CreatedWith = "RAGE2Toolkit v2.1.0",
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
        }

        public static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "untitled";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-') sb.Append('-');
            }
            var str = sb.ToString().Trim('-');
            while (str.Contains("--")) str = str.Replace("--", "-");
            return string.IsNullOrEmpty(str) ? "untitled" : str;
        }
    }
}
