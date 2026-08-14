using System;
using System.Collections;
using UnityEngine;

namespace MetalRaptors
{
    public class LevelIntro : MonoBehaviour
    {
        const float EntryMargin = 90f;
        const float CueFraction = 0.55f;
        const float HandoverFraction = 0.3f;

        CubeController _plane;
        PlaneShooter _shooter;
        Transform _tr;
        Action _onCue;
        float _cueX;
        float _handoverX;
        bool _active = true;

        public bool Active => _active;

        public static LevelIntro Begin(GameObject owner, CubeController plane, PlaneShooter shooter,
            float holdX, float halfViewWidth, Action onCue)
        {
            if (plane == null) return null;

            var intro = owner.AddComponent<LevelIntro>();
            intro._plane = plane;
            intro._shooter = shooter;
            intro._tr = plane.transform;
            intro._onCue = onCue;
            intro._cueX = holdX - halfViewWidth * CueFraction;
            intro._handoverX = holdX - halfViewWidth * HandoverFraction;

            Vector3 pos = intro._tr.position;
            intro._tr.position = new Vector3(holdX - halfViewWidth - EntryMargin, pos.y, pos.z);

            plane.SetControlled(false);
            if (shooter != null) shooter.Stop();

            intro.StartCoroutine(intro.Run());
            return intro;
        }

        IEnumerator Run()
        {
            while (_tr != null && _tr.position.x < _cueX) yield return null;
            if (_tr == null) yield break;

            _onCue?.Invoke();

            while (_tr != null && _tr.position.x < _handoverX) yield return null;

            Handover();
        }

        void Handover()
        {
            _active = false;
            if (_plane != null) _plane.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            Destroy(this);
        }
    }
}
