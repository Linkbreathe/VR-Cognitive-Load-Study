using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;

namespace EvidenceVR
{
    [Serializable] public sealed class ContinuousMediaReservation
    {
        public string session, stage, playlistId, playlistHash, catalogVersion, utcReserved;
        public int seed, previousReservations;
    }
    [Serializable] public sealed class ContinuousMediaHistory
    {
        public string revision = ContinuousPlaylistAllocation.Revision;
        public ContinuousMediaReservation[] reservations = Array.Empty<ContinuousMediaReservation>();
    }

    // Reservations are conservative: an aborted preparation still consumes its allocation.
    // Actual decoded exposure is recorded separately by AdaptiveMedia.
    public static class ContinuousPlaylistAllocation
    {
        public const string Revision = "participant-film-pool-v1";

        public static ContinuousMediaReservation Choose(ContinuousMediaBank bank, ContinuousMediaHistory history,
            string session, string stage, int seed)
        {
            if (bank.selectionRevision != Revision || bank.randomizedPlaylistIds == null ||
                bank.randomizedPlaylistIds.Length < 3 || bank.randomizedPlaylistIds.Distinct().Count() != bank.randomizedPlaylistIds.Length)
                throw new InvalidDataException("A validated film pool with at least three unique playlists is required.");
            if (history == null || history.revision != Revision || history.reservations == null ||
                history.reservations.Any(r => r == null || string.IsNullOrEmpty(r.playlistId) || string.IsNullOrEmpty(r.playlistHash)))
                throw new InvalidDataException("Invalid media allocation history; restore it before continuing.");
            var pool = bank.randomizedPlaylistIds.Select(id => bank.playlists.Single(p => p.id == id)).OrderBy(p => p.id, StringComparer.Ordinal).ToArray();
            if (pool.Select(p => p.sha256).Distinct().Count() != pool.Length || pool.Any(p => string.IsNullOrWhiteSpace(p.sha256)))
                throw new InvalidDataException("The randomized pool contains duplicate or missing content hashes.");
            var segments = pool.SelectMany(p => p.segments ?? Array.Empty<ContinuousSegment>()).ToArray();
            if (pool.Any(p => p.segments == null || p.segments.Length == 0) || segments.Select(s => s.mediaId).Distinct().Count() != segments.Length ||
                segments.Select(s => s.sourceVideoHash).Distinct().Count() != segments.Length)
                throw new InvalidDataException("Randomized playlists must contain disjoint source excerpts.");
            var prior = history.reservations.SingleOrDefault(r => r.session == session && r.stage == stage);
            if (prior != null)
            {
                if (!pool.Any(p => p.id == prior.playlistId && p.sha256 == prior.playlistHash))
                    throw new InvalidDataException("The reserved media changed. Start a fresh session.");
                return prior;
            }
            Func<ContinuousPlaylist, int> count = p => history.reservations.Count(r => r.playlistHash == p.sha256);
            int least = pool.Min(count);
            var candidates = pool.Where(p => count(p) == least).ToArray();
            var recent = history.reservations.Skip(Math.Max(0, history.reservations.Length - 2)).Select(r => r.playlistHash).ToArray();
            var withoutRecent = candidates.Where(p => !recent.Contains(p.sha256)).ToArray();
            if (withoutRecent.Length > 0) candidates = withoutRecent;
            var chosen = candidates[new StableRandom(seed).Next(candidates.Length)];
            return new ContinuousMediaReservation { session = session, stage = stage, seed = seed,
                playlistId = chosen.id, playlistHash = chosen.sha256, catalogVersion = bank.catalogVersion,
                previousReservations = least, utcReserved = DateTime.UtcNow.ToString("O") };
        }

        public static ContinuousMediaReservation Reserve(string historyRoot, string scope, ContinuousMediaBank bank,
            string session, string stage, int seed, string sessionDirectory)
        {
            string key;
            using (var hash = SHA256.Create()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope))).Replace("-", "").ToLowerInvariant();
            Directory.CreateDirectory(historyRoot);
            string path = Path.Combine(historyRoot, key + ".json");
            // An exclusive lock prevents concurrent Unity processes from reserving the same unseen item.
            using (var guard = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var history = File.Exists(path) ? JsonUtility.FromJson<ContinuousMediaHistory>(File.ReadAllText(path)) : new ContinuousMediaHistory();
                var choice = Choose(bank, history, session, stage, seed);
                if (!history.reservations.Contains(choice))
                {
                    history.reservations = history.reservations.Concat(new[] { choice }).ToArray();
                    string temp = path + ".tmp";
                    File.WriteAllText(temp, JsonUtility.ToJson(history, true));
                    if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                }
                File.WriteAllText(Path.Combine(sessionDirectory, "continuous-media-selection.json"), JsonUtility.ToJson(choice, true));
                return choice;
            }
        }
    }
}
