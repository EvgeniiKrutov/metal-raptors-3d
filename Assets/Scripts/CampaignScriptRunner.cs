using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public interface ICampaignScriptHost
    {
        bool IsOver { get; }
        int EnemiesAlive { get; }
        bool CompanionReady { get; }
        void SpawnWave(EnemyGroup[] groups);
        float WarnIncoming(int planes);
        void ShowTask(string text);
        float CompleteTask();
        void CompleteLevel();
    }

    public class CampaignScriptRunner : MonoBehaviour
    {
        const float HoldMin = 0.8f;
        const float SilentWaveLeadSec = 1f;
        const int PairSize = 2;

        CampaignScript _script;
        ICampaignScriptHost _host;
        DialogueBar _bar;
        bool _stopped;
        int _skipFrame = -1;
        bool _warnedFirst;
        bool _warnedPair;

        public static CampaignScriptRunner Begin(GameObject owner, CampaignScript script,
            ICampaignScriptHost host, DialogueBar bar)
        {
            if (script == null || host == null) return null;

            var runner = owner.AddComponent<CampaignScriptRunner>();
            runner._script = script;
            runner._host = host;
            runner._bar = bar;
            runner.StartCoroutine(runner.Run());
            return runner;
        }

        public void Stop()
        {
            _stopped = true;
            StopAllCoroutines();
            if (_bar != null) _bar.Hide();
        }

        bool Running => !_stopped && _host != null && !_host.IsOver;

        IEnumerator Run()
        {
            foreach (CampaignStep step in _script.Steps)
            {
                if (!Running) yield break;

                if (step.op != CampaignOp.Say) yield return CloseBar();

                switch (step.op)
                {
                    case CampaignOp.Wait:
                        yield return Wait(step.seconds);
                        break;

                    case CampaignOp.Say:
                        yield return Say(step);
                        break;

                    case CampaignOp.Task:
                        _host.ShowTask(step.text);
                        break;

                    case CampaignOp.TaskDone:
                        yield return Wait(_host.CompleteTask());
                        break;

                    case CampaignOp.Spawn:
                        yield return Warn(step.groups);
                        _host.SpawnWave(step.groups);
                        break;

                    case CampaignOp.Wave:
                        yield return Warn(step.groups);
                        _host.SpawnWave(step.groups);
                        yield return WaitForClear();
                        break;

                    case CampaignOp.WaitClear:
                        yield return WaitForClear();
                        break;

                    case CampaignOp.Finish:
                        if (Running) _host.CompleteLevel();
                        yield break;
                }
            }

            yield return CloseBar();
        }

        IEnumerator CloseBar()
        {
            if (_bar == null || !_bar.IsOpen) yield break;

            _bar.Hide();
            yield return Wait(CinematicBars.SlideSec);
        }

        IEnumerator Say(CampaignStep step)
        {
            if (_bar == null)
            {
                yield return Wait(step.seconds);
                yield break;
            }

            if (!_bar.IsOpen)
            {
                _bar.Open();
                while (Running && !_bar.IsReady) yield return null;
                while (Running && !_host.CompanionReady) yield return null;
                yield return Wait(DialogueBar.LeadInSec);
                if (!Running) yield break;
            }

            List<string> parts = _bar.Split(step.speaker, step.text);

            int chars = 0;
            foreach (string part in parts) chars += part.Length;

            foreach (string part in parts)
            {
                if (!Running) yield break;
                yield return Speak(step, part, chars > 0 ? (float)part.Length / chars : 1f);
            }

            _bar.ClearLine();
        }

        IEnumerator Speak(CampaignStep step, string part, float share)
        {
            _bar.Show(step.speaker, part);

            float typing = 0f;
            bool skipped = false;
            while (Running && _bar.IsRevealing)
            {
                if (Skipped()) { skipped = true; break; }

                _bar.Reveal(Time.deltaTime);
                typing += Time.deltaTime;
                yield return null;
            }

            float hold = skipped ? 0f : Mathf.Max(step.seconds * share - typing, HoldMin);
            while (hold > 0f && Running)
            {
                if (Skipped()) { skipped = true; break; }

                hold -= Time.deltaTime;
                yield return null;
            }

            if (skipped) yield return null;
        }

        bool Skipped()
        {
            if (Time.frameCount == _skipFrame) return false;
            if (GameMenu.IsOpen || LevelBriefing.IsOpen || ScreenFade.IsBusy) return false;
            if (!MenuInput.ReadSkip()) return false;

            _skipFrame = Time.frameCount;
            return true;
        }

        IEnumerator Warn(EnemyGroup[] groups)
        {
            int planes = 0;
            if (groups != null)
                foreach (EnemyGroup group in groups) planes += group.count;

            if (planes <= 0) yield break;

            bool announce = !_warnedFirst || (!_warnedPair && planes >= PairSize);

            _warnedFirst = true;
            if (planes >= PairSize) _warnedPair = true;

            yield return Wait(announce ? _host.WarnIncoming(planes) : SilentWaveLeadSec);
        }

        IEnumerator Wait(float seconds)
        {
            float left = seconds;
            while (left > 0f && Running)
            {
                left -= Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator WaitForClear()
        {
            yield return null;
            while (Running && _host.EnemiesAlive > 0) yield return null;
        }
    }
}
