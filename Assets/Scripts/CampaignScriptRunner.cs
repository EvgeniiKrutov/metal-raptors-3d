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
        void ArmSupply(bool open);
        void SetCompanionFoe(PlaneModelConfig plane);
        bool SpawnZeppelin();
        bool ZeppelinHanging { get; }
        void CompleteLevel();
        void Checkpoint(int step, bool warnedFirst, bool warnedPair);
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
        bool _frozen;
        int _skipFrame = -1;
        bool _warnedFirst;
        bool _warnedPair;
        int _start;
        int _firstSay = -1;
        bool _resumed;

        public static CampaignScriptRunner Begin(GameObject owner, CampaignScript script,
            ICampaignScriptHost host, DialogueBar bar, CampaignSnapshot resume = null)
        {
            if (script == null || host == null) return null;

            var runner = owner.AddComponent<CampaignScriptRunner>();
            runner._script = script;
            runner._host = host;
            runner._bar = bar;
            runner._firstSay = FirstSay(script);

            if (resume != null)
            {
                runner._start = Mathf.Clamp(resume.step, 0, script.Steps.Length);
                runner._warnedFirst = resume.warnedFirst;
                runner._warnedPair = resume.warnedPair;
                runner._resumed = true;
            }

            runner.StartCoroutine(runner.Run());
            return runner;
        }

        static int FirstSay(CampaignScript script)
        {
            for (int i = 0; i < script.Steps.Length; i++)
                if (script.Steps[i].op == CampaignOp.Say) return i;
            return -1;
        }

        public void Stop()
        {
            _stopped = true;
            _frozen = false;
            StopAllCoroutines();
            if (_bar != null) _bar.Hide();
            Wake();
        }

        void OnDestroy() => Wake();

        static void Wake()
        {
            CutsceneBlur.Clear();
            CutscenePause.Release();
        }

        bool Running => !_stopped && _host != null && !_host.IsOver;

        IEnumerator Run()
        {
            CampaignStep[] steps = _script.Steps;
            for (int index = _start; index < steps.Length; index++)
            {
                CampaignStep step = steps[index];
                if (!Running) yield break;

                if (step.op != CampaignOp.Say)
                {
                    _resumed = false;
                    yield return CloseBar();
                }

                switch (step.op)
                {
                    case CampaignOp.Wait:
                        yield return Wait(step.seconds);
                        break;

                    case CampaignOp.Say:
                        yield return Say(step, index);
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

                    case CampaignOp.Supply:
                        _host.ArmSupply(true);
                        break;

                    case CampaignOp.Foe:
                        _host.SetCompanionFoe(step.plane);
                        break;

                    case CampaignOp.Zeppelin:
                        if (_host.SpawnZeppelin()) yield return WaitForZeppelin();
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

            yield return Unblur();
            yield return Unfreeze();

            _bar.Hide();
            yield return Wait(CinematicBars.SlideSec);
        }

        IEnumerator Freeze()
        {
            if (_frozen) yield break;
            _frozen = true;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + CutscenePause.Delta / CutscenePause.FreezeSec, 1f);

                float k = Mathf.SmoothStep(0f, 1f, t);
                CutscenePause.Hold(1f - k);
                CutsceneBlur.Set(k);
                yield return null;
            }
        }

        void FreezeInstant()
        {
            _frozen = true;
            CutscenePause.Hold(0f);
            CutsceneBlur.Set(1f);
        }

        IEnumerator Unblur()
        {
            if (!_frozen) yield break;

            float t = 1f;
            while (t > 0f)
            {
                t = Mathf.Max(t - CutscenePause.Delta / CutsceneBlur.FadeSec, 0f);
                CutsceneBlur.Set(Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        IEnumerator Unfreeze()
        {
            if (!_frozen) yield break;
            _frozen = false;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(t + CutscenePause.Delta / CutscenePause.ThawSec, 1f);
                CutscenePause.Hold(Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            CutscenePause.Release();
        }

        IEnumerator Say(CampaignStep step, int index)
        {
            if (_bar == null)
            {
                yield return Wait(step.seconds);
                yield break;
            }

            if (!_bar.IsOpen)
            {
                _host.ArmSupply(false);

                if (_resumed)
                {
                    _resumed = false;
                    _bar.OpenInstant();
                    FreezeInstant();
                }
                else
                {
                    _bar.Open();
                    while (Running && !_bar.IsReady) yield return null;
                    while (Running && !_host.CompanionReady) yield return null;
                    if (!Running) yield break;

                    yield return Freeze();
                    if (!Running) yield break;
                    if (index > _firstSay) _host.Checkpoint(index, _warnedFirst, _warnedPair);
                }

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

                _bar.Reveal(CutscenePause.Delta);
                typing += CutscenePause.Delta;
                yield return null;
            }

            float hold = skipped ? 0f : Mathf.Max(step.seconds * share - typing, HoldMin);
            while (hold > 0f && Running)
            {
                if (Skipped()) { skipped = true; break; }

                hold -= CutscenePause.Delta;
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
                foreach (EnemyGroup group in groups)
                    if (group.kind == EnemyKind.Plane) planes += group.count;

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
                left -= CutscenePause.Delta;
                yield return null;
            }
        }

        IEnumerator WaitForClear()
        {
            yield return null;
            while (Running && _host.EnemiesAlive > 0) yield return null;
        }

        IEnumerator WaitForZeppelin()
        {
            while (Running && !_host.ZeppelinHanging) yield return null;
        }
    }
}
