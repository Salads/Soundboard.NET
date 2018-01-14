﻿using CSCore;
using CSCore.Codecs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soundboard
{
	public class SoundPlayer
	{
		private float m_Volume;
		private List<ISoundOut> m_PlayingSounds = new List<ISoundOut>();
		private IList<AudioDevice> m_playbackDevices;

		public bool IsPlaying { get { return m_PlayingSounds.Any(); } }

		public float VolumeNormalized 
		{
			get { return m_Volume; }
			set
			{
				if(value > 1.0f) { value = 1.0f; }
				if(value < 0.0f) { value = 0.0f; }

				m_Volume = value;
				foreach(ISoundOut PlayingSound in m_PlayingSounds)
				{
					PlayingSound.Volume = value;
				}
			}
		}

		public SoundPlayer()
		{
			m_playbackDevices = SoundboardSettings.Instance.SelectedPlaybackDevices;
			VolumeNormalized = SoundboardSettings.Instance.VolumeNormalized;
		}

		public void SetPlaybackDevices(IList<AudioDevice> devices)
		{
			// If we want to set it back to default for some reason
			if(ReferenceEquals(devices, SoundboardSettings.Instance.SelectedPlaybackDevices))
			{
				m_playbackDevices = SoundboardSettings.Instance.SelectedPlaybackDevices;
			}
			else
			{
				m_playbackDevices = devices;
			}
		}

		public void SetPlaybackDevice(AudioDevice device)
		{
			m_playbackDevices = new List<AudioDevice>
			{
				device
			};
		}

		public void Play(Sound sound, TimeSpan? startTime = null)
		{
			TimeSpan soundStartTime = ((startTime == null) ? sound.StartTime : startTime.Value);

			Debug.WriteLine("Playing new sound on devices: ");

			string filename = sound.FullFilepath;
			foreach(AudioDevice device in m_playbackDevices)
			{
				if(device == null) continue;

				Debug.Write(device.FriendlyName + ", ");

				//	Need to create one for every output because the stream is handled in WaveSource.
				//	This means multiple outputs will advance stream position if we don't seperate them.
				IWaveSource waveSource = CodecFactory.Instance.GetCodec(filename)
					.ToSampleSource()
					.ToStereo()
					.ToWaveSource();
				waveSource.SetPosition(soundStartTime);

				ISoundOut newSoundOut = new WasapiOut() { Latency = 20, Device = device.Info };
				newSoundOut.Initialize(waveSource);
				newSoundOut.Stopped += EV_OnSoundStopped;
				newSoundOut.Volume = VolumeNormalized;
				newSoundOut.Play();

				m_PlayingSounds.Add(newSoundOut);
				SoundboardSettings.Instance.SetMicMuted(SoundboardSettings.Instance.MuteMicrophoneWhilePlaying);
			}

			Debug.WriteLine("");
		}

		public void StopSoundsOnDevice(AudioDevice device)
		{
			if(device == null) return;

			foreach(ISoundOut soundOut in m_PlayingSounds)
			{
				WasapiOut baseOut = soundOut as WasapiOut;
				if(baseOut == null) return;

				if(device.DeviceID == baseOut.Device.DeviceID)
				{
					_StopSound(soundOut);
				}
			}
		}

		public void StopAllSounds()
		{
			foreach(ISoundOut PlayingSound in m_PlayingSounds)
			{
				// Run a task here to make sounds stop more in time with each other.
				Task.Run(() => _StopSound(PlayingSound));
			}

			SoundboardSettings.Instance.SetMicMuted(false);

			m_PlayingSounds.Clear();
		}

		private void _StopSound(ISoundOut Sound)
		{
			Debug.WriteLine("Stop Sound Called");

			if(Sound.PlaybackState != PlaybackState.Stopped)
			{
				Sound.Stop();
				Sound.Dispose();
			}
		}

		private void EV_OnSoundStopped(object sender, PlaybackStoppedEventArgs e)
		{
			ISoundOut Stopped = sender as ISoundOut;
			m_PlayingSounds.Remove(Stopped);

			if(!m_PlayingSounds.Any())
			{
				SoundboardSettings.Instance.SetMicMuted(false);
			}

			Stopped.Dispose();
		}
	}
}