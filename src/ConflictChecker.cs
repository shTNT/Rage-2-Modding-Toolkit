using System;
using System.Collections.Generic;

namespace Rage2Toolkit
{
    public class ConflictEntry
    {
        public string HashHex { get; set; }
        public string ExistingModId { get; set; }
        public string ExistingModName { get; set; }
        public string ExistingModVersion { get; set; }
        public int ExistingPriority { get; set; }
        public string IncomingModId { get; set; }
        public int IncomingPriority { get; set; }
    }

    public class ConflictReport
    {
        public List<ConflictEntry> Conflicts { get; set; } = new List<ConflictEntry>();
        public bool HasConflicts { get { return Conflicts.Count > 0; } }
    }

    public static class ConflictChecker
    {
        public static ConflictReport Check(string toolkitRoot, IEnumerable<string> newHashes, string incomingModId, int incomingPriority)
        {
            var report = new ConflictReport();
            var store = ModsStore.Load(toolkitRoot);

            // Recopilar hashes normalizados del incoming
            var incoming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in newHashes)
            {
                if (string.IsNullOrEmpty(h)) continue;
                incoming.Add(h.ToUpperInvariant());
            }

            foreach (var m in store.Installed)
            {
                if (m.Hashes == null) continue;
                foreach (var h in m.Hashes)
                {
                    if (!incoming.Contains(h.ToUpperInvariant())) continue;
                    report.Conflicts.Add(new ConflictEntry
                    {
                        HashHex = h.ToUpperInvariant(),
                        ExistingModId = m.Id,
                        ExistingModName = m.Name,
                        ExistingModVersion = m.Version,
                        ExistingPriority = m.Priority,
                        IncomingModId = incomingModId,
                        IncomingPriority = incomingPriority,
                    });
                }
            }
            return report;
        }
    }
}
