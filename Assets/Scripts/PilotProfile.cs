using UnityEngine;

namespace MetalRaptors
{
    public readonly struct RankResult
    {
        public readonly int Before;
        public readonly int After;
        public readonly bool Replay;

        public RankResult(int before, int after, bool replay)
        {
            Before = before;
            After = after;
            Replay = replay;
        }
    }

    public static class PilotProfile
    {
        const string PrefPoints = "profile_points";
        const string PrefFinishedPrefix = "profile_finished_";
        const float ReplayShare = 0.5f;

        static bool _loaded;
        static int _points;
        static bool _scoring;
        static int _run;
        static RankResult? _result;

        public static int Points
        {
            get
            {
                Load();
                return _points;
            }
        }

        public static int RankIndex => PilotRanks.IndexFor(Points);

        public static int RunPoints => _run;

        public static void BeginRun(bool scoring, int carried)
        {
            _scoring = scoring;
            _run = scoring ? Mathf.Max(0, carried) : 0;
            _result = null;
        }

        public static void Score(int points)
        {
            if (_scoring && points > 0) _run += points;
        }

        public static void Finish(int level)
        {
            if (!_scoring) return;
            _scoring = false;
            Load();

            string key = PrefFinishedPrefix + level;
            bool replay = PlayerPrefs.GetInt(key, 0) != 0;
            int earned = replay ? Mathf.FloorToInt(_run * ReplayShare) : _run;
            int before = _points;

            _points += earned;
            _run = 0;
            PlayerPrefs.SetInt(PrefPoints, _points);
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

            _result = new RankResult(before, _points, replay);
        }

        public static bool TakeResult(out RankResult result)
        {
            result = _result ?? default;
            bool has = _result.HasValue;
            _result = null;
            return has;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _points = Mathf.Max(0, PlayerPrefs.GetInt(PrefPoints, 0));
        }
    }
}
