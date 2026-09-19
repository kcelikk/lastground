using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Poses a model instance at a clip time for baking. Legacy/generic clips write transforms directly
    /// (<see cref="AnimationClip.SampleAnimation"/>); Humanoid clips go through the instance's Animator in a manual
    /// PlayableGraph, so a motion recorded on one skeleton lands on the body's own proportions.
    /// </summary>
    sealed class PoseSampler : IDisposable
    {
        readonly GameObject _root;
        readonly IReadOnlyList<AnimationClip> _clips;
        readonly PlayableGraph _graph;
        readonly AnimationClipPlayable[] _playables;
        readonly AnimationPlayableOutput _output;
        readonly bool _humanoid;

        public PoseSampler(GameObject root, IReadOnlyList<AnimationClip> clips, bool humanoid)
        {
            _root = root;
            _clips = clips;
            _humanoid = humanoid;
            if (!humanoid) return;
            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException(root.name + ": Humanoid body without a valid human avatar");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("CrowdBake");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _output = AnimationPlayableOutput.Create(_graph, "Pose", animator);
            _playables = new AnimationClipPlayable[clips.Count];
            for (int i = 0; i < clips.Count; i++)
            {
                _playables[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                _playables[i].SetApplyFootIK(false);
                _playables[i].SetApplyPlayableIK(false);
            }
        }

        public void Sample(int clip, float time)
        {
            if (!_humanoid)
            {
                _clips[clip].SampleAnimation(_root, time);
                return;
            }
            _output.SetSourcePlayable(_playables[clip]);
            _playables[clip].SetTime(time);
            _graph.Evaluate();
        }

        public void Dispose()
        {
            if (_humanoid && _graph.IsValid()) _graph.Destroy();
        }
    }
}
