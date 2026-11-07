// Copyright (c) 2026 LaserGRBL contributors
// This file is part of LaserGRBL and is released under the GNU GPLv3.

using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LaserGRBL
{
	/// <summary>
	/// Starts LaserGRBL with Windows, full screen, when the user turns the setting on.
	/// The run key is per user, so it works without administrator rights on Windows 7, 10 and 11.
	/// </summary>
	public static class AutoStart
	{
		public const string SettingKey = "Autostart";
		private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
		private const string ValueName = "LaserGRBL";

		public static bool IsEnabled
		{
			get { return Settings.GetObject(SettingKey, false); }
		}

		public static void Apply(bool enabled)
		{
			Settings.SetObject(SettingKey, enabled);
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, true))
				{
					if (key == null)
						return;
					if (!enabled)
					{
						key.DeleteValue(ValueName, false);
						return;
					}
					string exe = Application.ExecutablePath;
					key.SetValue(ValueName, "\"" + exe + "\"");
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
