using UnityEngine;

namespace NHSRemont.Utility
{
    [CreateAssetMenu(fileName = "New SFXCollection", menuName = "Collections/SFXCollection")]
    public class SFXCollection : ScriptableObject
    {
        public AudioClip[] clips;
        public UnityEngine.Audio.AudioMixerGroup mixerGroup;
        public float volume = 1f;
        [Tooltip("The volume will be randomised by this amount (+/- absolute)")]
        public float randomiseVol = 0f;
        public float pitch = 1f;
        [Tooltip("The pitch will be randomised by this amount (+/- absolute)")]
        public float randomisePitch = 0f;
        public float rangeMin = 5f;
        public float rangeMax = 500f;
        [Tooltip("If true, the sound will be heard everywhere at full volume without consideration of its physical position.")]
        public bool omnipresent = false; //use spatial blend 2D?

        public AudioSource PlaySoundAtPosition(Vector3 pos, int index = -1, float volMult = 1f, float pitchMult = 1f, float rangeMult = 1f)
        {
            if (index == -1) index = PickRandomIndex();
            else index = Mathf.Clamp(index, 0, clips.Length - 1);

            Transform sfx = new GameObject("sfx_" + name + "_" + index).transform;
            sfx.position = pos;
            AudioSource a = sfx.gameObject.AddComponent<AudioSource>();
            a.clip = clips[index];
            a.outputAudioMixerGroup = mixerGroup;
            a.volume = GetVolRandomised() * volMult;
            a.pitch = GetPitchRandomised() * pitchMult;
            a.minDistance = rangeMin*rangeMult;
            a.maxDistance = rangeMax*rangeMult;
            a.spatialBlend = omnipresent ? 0 : 1;
            a.Play();

            sfx.gameObject.AddComponent<Autodestroy>().destroyTimer = a.clip.length / a.pitch;

            return a;
        }
        public AudioSource PlayRandomSoundAtPosition(Vector3 pos, float volMult = 1f, float pitchMult = 1f, float rangeMult = 1f)
        {
            return PlaySoundAtPosition(pos, PickRandomIndex(), volMult, pitchMult, rangeMult);
        }

        public void PlaySoundSeriesAtPosition(Vector3 pos, float volMult = 1f, float pitchMult = 1f, float rangeMult = 1f, params int[] indices)
        {
            foreach (int index in indices)
            {
                PlaySoundAtPosition(pos, index, volMult, pitchMult, rangeMult);
            }
        }
        public void PlayRandomSoundSeriesAtPosition(Vector3 pos, int amount, float volMult = 1f, float pitchMult = 1f, float rangeMult = 1f)
        {
            int[] indices = new int[amount];
            for (int i = 0; i < amount; i++)
            {
                indices[i] = PickRandomIndex();
            }
            PlaySoundSeriesAtPosition(pos, volMult, pitchMult, rangeMult, indices);
        }

        private float GetVolRandomised()
        {
            return volume + Random.Range(-randomiseVol, randomiseVol);
        }

        private float GetPitchRandomised()
        {
            return pitch + Random.Range(-randomisePitch, randomisePitch);
        }

        private int PickRandomIndex()
        {
            return Random.Range(0, clips.Length);
        }

    }
}