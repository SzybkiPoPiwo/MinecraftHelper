using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MinecraftHelper.Services
{
    internal static class MiningLogKinds
    {
        public const string ItemsDiscarded = "items-discarded";
        public const string CobbleXCreated = "cobblex-created";
        public const string InventorySession = "inventory-session";
    }

    internal static class MiningLogStatuses
    {
        public const string InProgress = "in-progress";
        public const string Completed = "completed";
        public const string Aborted = "aborted";
        public const string Interrupted = "interrupted";
    }

    internal sealed class MiningLogItemDetail
    {
        public string ItemId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int StackCount { get; set; }
    }

    internal sealed class MiningLogEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Kind { get; set; } = string.Empty;
        // Count is kept for compatibility with log files created before 1.1.0.
        // New item-discard entries use ItemCount and StackCount for exact totals.
        public int Count { get; set; }
        public int ItemCount { get; set; }
        public int StackCount { get; set; }
        public string Details { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? CompletedAt { get; set; }
        public int DropPasses { get; set; }
        public int RemainingStacks { get; set; }
        public int FullCobblestoneStacks { get; set; }
        public int RequiredCobblestoneStacks { get; set; }
        public bool CobbleXCreated { get; set; }
        public string CobbleXCommand { get; set; } = string.Empty;
        public List<MiningLogItemDetail> Items { get; set; } = new List<MiningLogItemDetail>();
    }

    internal readonly record struct MiningLogSummary(
        int CobbleXCreated,
        int DiscardedItems,
        int DiscardedStacks,
        int LegacyDiscardedStacks,
        int InventorySessions,
        int FailedInventorySessions,
        DateTimeOffset? FirstActivity,
        DateTimeOffset? LastActivity);

    internal sealed class MiningLogService
    {
        private const string LogsFolderName = "Minecraft Helper";
        private const string LogsFileName = "mining-logs.json";
        private const int MaximumEntries = 5000;

        private readonly object _sync = new object();
        private readonly List<MiningLogEntry> _entries;

        public event EventHandler? Changed;

        public MiningLogService()
        {
            _entries = LoadEntries();
            FinalizeInterruptedSessions();
        }

        public string LogsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LogsFolderName,
            LogsFileName);

        public IReadOnlyList<MiningLogEntry> GetEntriesNewestFirst()
        {
            lock (_sync)
            {
                return _entries
                    .OrderByDescending(entry => entry.Timestamp)
                    .Select(CloneEntry)
                    .ToList();
            }
        }

        public MiningLogSummary GetSummary()
        {
            lock (_sync)
            {
                int cobbleXCreated = _entries
                    .Sum(entry => entry.Kind == MiningLogKinds.CobbleXCreated
                        ? Math.Max(0, entry.Count)
                        : entry.Kind == MiningLogKinds.InventorySession && entry.CobbleXCreated ? 1 : 0);
                int discardedItems = _entries
                    .Where(IsDiscardEntry)
                    .Sum(entry => Math.Max(0, entry.ItemCount));
                int discardedStacks = _entries
                    .Where(IsDiscardEntry)
                    .Sum(GetDiscardedStackCount);
                int legacyDiscardedStacks = _entries
                    .Where(entry => entry.Kind == MiningLogKinds.ItemsDiscarded && entry.ItemCount <= 0)
                    .Sum(entry => Math.Max(0, entry.Count));
                int inventorySessions = _entries.Count(entry => entry.Kind == MiningLogKinds.InventorySession);
                int failedInventorySessions = _entries.Count(entry =>
                    entry.Kind == MiningLogKinds.InventorySession
                    && entry.Status is MiningLogStatuses.Aborted or MiningLogStatuses.Interrupted);
                DateTimeOffset? firstActivity = _entries.Count > 0
                    ? _entries.Min(entry => entry.Timestamp)
                    : null;
                DateTimeOffset? lastActivity = _entries.Count > 0
                    ? _entries.Max(entry => entry.Timestamp)
                    : null;

                return new MiningLogSummary(
                    cobbleXCreated,
                    discardedItems,
                    discardedStacks,
                    legacyDiscardedStacks,
                    inventorySessions,
                    failedInventorySessions,
                    firstActivity,
                    lastActivity);
            }
        }

        public bool StartInventorySession(string owner, out string sessionId, out string error)
        {
            sessionId = Guid.NewGuid().ToString("N");
            lock (_sync)
            {
                var entry = new MiningLogEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    Kind = MiningLogKinds.InventorySession,
                    SessionId = sessionId,
                    Owner = owner?.Trim() ?? string.Empty,
                    Status = MiningLogStatuses.InProgress,
                    Details = "Otwarto ekwipunek. Oczekiwanie na skan i wynik czyszczenia."
                };

                _entries.Add(entry);
                TrimEntries();
                if (TrySave(out error))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _entries.Remove(entry);
                sessionId = string.Empty;
                return false;
            }
        }

        public bool CompleteInventorySession(
            string sessionId,
            string owner,
            int itemCount,
            int stackCount,
            IReadOnlyList<MiningLogItemDetail> items,
            int dropPasses,
            int remainingStacks,
            bool cobbleXCreated,
            string cobbleXCommand,
            int fullCobblestoneStacks,
            int requiredCobblestoneStacks,
            string details,
            out string error)
        {
            lock (_sync)
            {
                MiningLogEntry entry = FindOrCreateInventorySession(sessionId, owner);
                MiningLogEntry backup = CloneEntry(entry);

                entry.Owner = owner?.Trim() ?? string.Empty;
                entry.Status = MiningLogStatuses.Completed;
                entry.CompletedAt = DateTimeOffset.Now;
                entry.ItemCount = Math.Max(0, itemCount);
                entry.StackCount = Math.Max(0, stackCount);
                entry.Count = entry.StackCount;
                entry.DropPasses = Math.Max(0, dropPasses);
                entry.RemainingStacks = Math.Max(0, remainingStacks);
                entry.CobbleXCreated = cobbleXCreated;
                entry.CobbleXCommand = cobbleXCommand?.Trim() ?? string.Empty;
                entry.FullCobblestoneStacks = Math.Max(0, fullCobblestoneStacks);
                entry.RequiredCobblestoneStacks = Math.Max(0, requiredCobblestoneStacks);
                entry.Details = details?.Trim() ?? string.Empty;
                entry.Items = (items ?? Array.Empty<MiningLogItemDetail>())
                    .Where(item => item != null && (item.ItemCount > 0 || item.StackCount > 0))
                    .Select(CloneItem)
                    .ToList();

                if (TrySave(out error))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                CopyEntry(backup, entry);
                return false;
            }
        }

        public bool AbortInventorySession(
            string sessionId,
            string owner,
            string reason,
            int itemCount,
            int stackCount,
            IReadOnlyList<MiningLogItemDetail> items,
            int dropPasses,
            int remainingStacks,
            int fullCobblestoneStacks,
            int requiredCobblestoneStacks,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                error = string.Empty;
                return true;
            }

            lock (_sync)
            {
                MiningLogEntry entry = FindOrCreateInventorySession(sessionId, owner);
                MiningLogEntry backup = CloneEntry(entry);
                entry.Owner = owner?.Trim() ?? string.Empty;
                entry.Status = MiningLogStatuses.Aborted;
                entry.CompletedAt = DateTimeOffset.Now;
                entry.ItemCount = Math.Max(0, itemCount);
                entry.StackCount = Math.Max(0, stackCount);
                entry.Count = entry.StackCount;
                entry.Items = (items ?? Array.Empty<MiningLogItemDetail>())
                    .Where(item => item != null && (item.ItemCount > 0 || item.StackCount > 0))
                    .Select(CloneItem)
                    .ToList();
                entry.DropPasses = Math.Max(0, dropPasses);
                entry.RemainingStacks = Math.Max(0, remainingStacks);
                entry.FullCobblestoneStacks = Math.Max(0, fullCobblestoneStacks);
                entry.RequiredCobblestoneStacks = Math.Max(0, requiredCobblestoneStacks);
                entry.Details = reason?.Trim() ?? "Sesja EQ została przerwana.";

                if (TrySave(out error))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                CopyEntry(backup, entry);
                return false;
            }
        }

        public bool RecordDiscardedItems(int itemCount, int stackCount, string details, out string error)
        {
            if (itemCount <= 0 || stackCount <= 0)
            {
                error = string.Empty;
                return true;
            }

            return AddEntry(
                MiningLogKinds.ItemsDiscarded,
                stackCount,
                Math.Max(0, itemCount),
                Math.Max(0, stackCount),
                details,
                out error);
        }

        public bool RecordCobbleXCreated(string details, out string error)
        {
            return AddEntry(MiningLogKinds.CobbleXCreated, 1, 0, 0, details, out error);
        }

        public bool Clear(out string error)
        {
            lock (_sync)
            {
                List<MiningLogEntry> backup = _entries.Select(CloneEntry).ToList();
                _entries.Clear();
                if (TrySave(out error))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _entries.AddRange(backup);
                return false;
            }
        }

        private bool AddEntry(
            string kind,
            int count,
            int itemCount,
            int stackCount,
            string details,
            out string error)
        {
            lock (_sync)
            {
                var entry = new MiningLogEntry
                {
                    Timestamp = DateTimeOffset.Now,
                    Kind = kind,
                    Count = Math.Max(0, count),
                    ItemCount = Math.Max(0, itemCount),
                    StackCount = Math.Max(0, stackCount),
                    Details = details?.Trim() ?? string.Empty
                };

                _entries.Add(entry);
                TrimEntries();

                if (TrySave(out error))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _entries.Remove(entry);
                return false;
            }
        }

        private List<MiningLogEntry> LoadEntries()
        {
            try
            {
                if (!File.Exists(LogsFilePath))
                    return new List<MiningLogEntry>();

                string json = File.ReadAllText(LogsFilePath);
                List<MiningLogEntry>? entries = JsonSerializer.Deserialize<List<MiningLogEntry>>(json);
                return (entries ?? new List<MiningLogEntry>())
                    .Where(entry => entry != null
                        && entry.Timestamp != default
                        && (entry.Kind == MiningLogKinds.ItemsDiscarded
                            || entry.Kind == MiningLogKinds.CobbleXCreated
                            || entry.Kind == MiningLogKinds.InventorySession)
                        && (entry.Kind == MiningLogKinds.InventorySession
                            || entry.Count > 0
                            || entry.ItemCount > 0
                            || entry.StackCount > 0))
                    .OrderBy(entry => entry.Timestamp)
                    .TakeLast(MaximumEntries)
                    .ToList();
            }
            catch
            {
                return new List<MiningLogEntry>();
            }
        }

        private bool TrySave(out string error)
        {
            string? temporaryPath = null;
            try
            {
                string directory = Path.GetDirectoryName(LogsFilePath) ?? string.Empty;
                Directory.CreateDirectory(directory);
                string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
                temporaryPath = Path.Combine(directory, $".{LogsFileName}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, LogsFilePath, overwrite: true);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryPath) && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                        // A leftover temporary file is harmless; the main log stays intact.
                    }
                }
            }
        }

        private static MiningLogEntry CloneEntry(MiningLogEntry entry)
        {
            return new MiningLogEntry
            {
                Timestamp = entry.Timestamp,
                Kind = entry.Kind,
                Count = entry.Count,
                ItemCount = entry.ItemCount,
                StackCount = entry.StackCount,
                Details = entry.Details,
                SessionId = entry.SessionId,
                Owner = entry.Owner,
                Status = entry.Status,
                CompletedAt = entry.CompletedAt,
                DropPasses = entry.DropPasses,
                RemainingStacks = entry.RemainingStacks,
                FullCobblestoneStacks = entry.FullCobblestoneStacks,
                RequiredCobblestoneStacks = entry.RequiredCobblestoneStacks,
                CobbleXCreated = entry.CobbleXCreated,
                CobbleXCommand = entry.CobbleXCommand,
                Items = (entry.Items ?? new List<MiningLogItemDetail>()).Select(CloneItem).ToList()
            };
        }

        private static MiningLogItemDetail CloneItem(MiningLogItemDetail item)
        {
            return new MiningLogItemDetail
            {
                ItemId = item.ItemId,
                Label = item.Label,
                ItemCount = item.ItemCount,
                StackCount = item.StackCount
            };
        }

        private static void CopyEntry(MiningLogEntry source, MiningLogEntry target)
        {
            MiningLogEntry clone = CloneEntry(source);
            target.Timestamp = clone.Timestamp;
            target.Kind = clone.Kind;
            target.Count = clone.Count;
            target.ItemCount = clone.ItemCount;
            target.StackCount = clone.StackCount;
            target.Details = clone.Details;
            target.SessionId = clone.SessionId;
            target.Owner = clone.Owner;
            target.Status = clone.Status;
            target.CompletedAt = clone.CompletedAt;
            target.DropPasses = clone.DropPasses;
            target.RemainingStacks = clone.RemainingStacks;
            target.FullCobblestoneStacks = clone.FullCobblestoneStacks;
            target.RequiredCobblestoneStacks = clone.RequiredCobblestoneStacks;
            target.CobbleXCreated = clone.CobbleXCreated;
            target.CobbleXCommand = clone.CobbleXCommand;
            target.Items = clone.Items;
        }

        private MiningLogEntry FindOrCreateInventorySession(string sessionId, string owner)
        {
            MiningLogEntry? entry = _entries.LastOrDefault(candidate =>
                candidate.Kind == MiningLogKinds.InventorySession
                && !string.IsNullOrWhiteSpace(sessionId)
                && string.Equals(candidate.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
                return entry;

            entry = new MiningLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Kind = MiningLogKinds.InventorySession,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId,
                Owner = owner?.Trim() ?? string.Empty,
                Status = MiningLogStatuses.InProgress
            };
            _entries.Add(entry);
            TrimEntries();
            return entry;
        }

        private void FinalizeInterruptedSessions()
        {
            lock (_sync)
            {
                bool changed = false;
                foreach (MiningLogEntry entry in _entries)
                {
                    if (entry.Kind != MiningLogKinds.InventorySession
                        || entry.Status != MiningLogStatuses.InProgress)
                        continue;

                    entry.Status = MiningLogStatuses.Interrupted;
                    entry.Details = "Sesja EQ nie została zakończona — program lub makro zostało wcześniej zatrzymane.";
                    changed = true;
                }

                if (changed)
                    _ = TrySave(out _);
            }
        }

        private void TrimEntries()
        {
            if (_entries.Count <= MaximumEntries)
                return;

            int removeCount = _entries.Count - MaximumEntries;
            _entries.RemoveRange(0, removeCount);
        }

        private static bool IsDiscardEntry(MiningLogEntry entry)
        {
            return entry.Kind == MiningLogKinds.ItemsDiscarded
                || entry.Kind == MiningLogKinds.InventorySession;
        }

        internal static int GetDiscardedStackCount(MiningLogEntry entry)
        {
            return entry.StackCount > 0
                ? entry.StackCount
                : Math.Max(0, entry.Count);
        }
    }
}
