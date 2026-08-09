using System.Collections;
using UnityEngine;

namespace MetalRaptors
{
    public interface ICampaignScriptHost
    {
        bool IsOver { get; }
        int EnemiesAlive { get; }
        void SpawnWave(EnemyGroup[] groups);
        void CompleteLevel();
    }

    public class CampaignScriptRunner : MonoBehaviour
    {
        CampaignScript _script;
        ICampaignScriptHost _host;
        DialogueBar _bar;
        bool _stopped;

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

                switch (step.op)
                {
                    case CampaignOp.Wait:
                        yield return Wait(step.seconds);
                        break;

                    case CampaignOp.Say:
                        if (_bar != null) _bar.Show(step.speaker, step.text);
                        yield return Wait(step.seconds);
                        if (_bar != null) _bar.Hide();
                        break;

                    case CampaignOp.Spawn:
                        _host.SpawnWave(step.groups);
                        break;

                    case CampaignOp.Wave:
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
