using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PomodoroTimer
{
	public static class AudioUtil
	{
		public static void PlayClip(AudioClip clip)
		{
			Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
			Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
			MethodInfo method = audioUtilClass.GetMethod(
				"PlayClip",
				BindingFlags.Static | BindingFlags.Public,
				null,
				new System.Type[]
				{
					typeof(AudioClip)
				},
				null
			);
			method.Invoke(
				null,
				new object[]
				{
					clip
				}
			);
		}
	}

	public class PomodoroTimer : EditorWindow
	{
		#region Initialization and Main Loop

		private double updateCounter;
		private AudioClip dingAudio, startTimerAudio, endAudio;

		private string dingAudioPath = "Pomodoro/ding.mp3";
		private string startTimerAudioPath = "Pomodoro/ticktock.mp3";
		private string endAudioPath = "Pomodoro/timeisup.mp3";

		private int repaintInSecond = 1;
		private int updateFrequency = 100; // set by unity

		private const float SecondsInMinute = 60;
		private const float DefaultTimerDuration = 0.4f * SecondsInMinute; // in seconds
		private const float DefaultRestTimerDuration = 0.1f * SecondsInMinute; // in seconds
		private Timer activeTimer = null;

		private float _warningTime = 0.2f; // as a percentage of total duration
		private bool clearSoundWarning = true;

		private bool isRunning = true;

		[MenuItem("Productivity/Pomodoro Timer")]
		static void Init()
		{
			PomodoroTimer window = (PomodoroTimer) GetWindow(typeof(PomodoroTimer));
			window.Show();
		}

		void OnEnable()
		{
			EditorApplication.update += EditorAppUpdate;

			dingAudio = EditorGUIUtility.Load(dingAudioPath) as AudioClip;
			startTimerAudio = EditorGUIUtility.Load(startTimerAudioPath) as AudioClip;
			endAudio = EditorGUIUtility.Load(endAudioPath) as AudioClip;
		}


		void EditorAppUpdate()
		{
			updateCounter++;
			if (updateCounter % (updateFrequency / repaintInSecond) == 0)
			{
				Repaint();
			}
		}

		#endregion

		void OnGUI()
		{
			if (activeTimer != null && !activeTimer.isInitialized
			) // unity serialization does not support null custom objects, so this is my fix to that.
			{
				activeTimer = null;
				return;
			}
			//GUILayout.Label(repaintInSecond + " ui update / second");

			EditorGUILayout.BeginHorizontal();
			{
				GUI.enabled = activeTimer == null;
				if (GUILayout.Button("Start"))
				{
					activeTimer = SetTimer();
					AudioUtil.PlayClip(startTimerAudio);
				}


				GUI.enabled = activeTimer != null && !activeTimer.isPaused;
				if (GUILayout.Button("Pause"))
				{
					activeTimer.Pause();
				}

				GUI.enabled = activeTimer != null && activeTimer.isPaused;
				if (GUILayout.Button("Resume"))
				{
					activeTimer.Resume();
				}

				GUI.enabled = activeTimer != null;
				if (GUILayout.Button("Stop"))
				{
					activeTimer.Stop();
					activeTimer = null;
					isRunning = true;
				}
			}
			EditorGUILayout.EndHorizontal();
			var remaining = DefaultTimerDuration;
			float elapsed = 0, progress = 0;
			bool giveWarning = false, soundWarning, colorWarning;
			if (activeTimer != null)
			{
				remaining = activeTimer.GetRemaining();
				elapsed = activeTimer.GetElapsed();
				giveWarning = remaining <= activeTimer.Duration * _warningTime;
				soundWarning = giveWarning;
				if (soundWarning && clearSoundWarning)
				{
					clearSoundWarning = false;
					AudioUtil.PlayClip(dingAudio);
				}

				progress = elapsed / (float) activeTimer.Duration;
			}

			GUILayout.BeginHorizontal();
			var rect = EditorGUILayout.GetControlRect();
			var ratio = DefaultTimerDuration / (DefaultRestTimerDuration + DefaultTimerDuration);
			var totalWidth = rect.width;
			rect.width = rect.width * (float) ratio;
			// work progress bar

			colorWarning = giveWarning;
			var guiColor = GUI.color;
			GUI.color = isRunning && colorWarning ? Color.red : guiColor;

			var workProgress = isRunning ? progress : 1;
			var workRemaining = isRunning ? remaining : DefaultTimerDuration;
			EditorGUI.ProgressBar(rect, workProgress, GetFormattedTime(workRemaining));

			rect.x += rect.width;
			rect.width = totalWidth - rect.width;
			// break progress bar
			GUI.color = !isRunning && colorWarning ? Color.red : guiColor;

			var restProgress = isRunning ? 0 : progress;
			var restRemaining = isRunning ? DefaultRestTimerDuration : remaining;
			EditorGUI.ProgressBar(rect, restProgress, GetFormattedTime(restRemaining));
			GUILayout.EndHorizontal();
			if (remaining <= 0)
			{
				activeTimer.Stop();
				isRunning = !isRunning;

				activeTimer = SetTimer();
				AudioUtil.PlayClip(endAudio);
				clearSoundWarning = true;
			}
		}

		private string GetFormattedTime(double seconds)
		{
			var span = System.TimeSpan.FromSeconds(seconds);
			return string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
		}

		Timer SetTimer()
		{
			var duration = isRunning ? DefaultTimerDuration : DefaultRestTimerDuration;
			return new Timer(duration, (float) EditorApplication.timeSinceStartup);
		}

		[System.Serializable]
		protected class Timer
		{
			public bool isInitialized;
			private float startTime;
			private float pauseTime;
			private float resumeTime;

			public float Duration { get; private set; }
			public bool isPaused;
			public bool isStopped;

			public Timer(float duration, float startTime)
			{
				isInitialized = true;
				Duration = duration;
				this.startTime = startTime;
			}

			public void Pause()
			{
				isPaused = true;
				pauseTime = (float) EditorApplication.timeSinceStartup;
			}

			public void Resume()
			{
				isPaused = false;
				resumeTime = (float) EditorApplication.timeSinceStartup;
				startTime += resumeTime - pauseTime;
			}

			public void Stop()
			{
				isStopped = true;
			}

			public float GetElapsed()
			{
				float elapsed = 0;
				if (isStopped)
				{
					elapsed = 0;
				}
				else if (isPaused)
				{
					elapsed = pauseTime - startTime;
				}
				else
				{
					elapsed = (float) EditorApplication.timeSinceStartup - startTime;
				}

				return elapsed;
			}

			public float GetRemaining()
			{
				return Duration - GetElapsed();
			}
		}
	}
}