﻿/*
 * Author : K.F.Storm
 * Email : yk000123 at sina.com
 * Website : http://www.kfstorm.com
 * */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Windows.Markup;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using DoubanFM.Core;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace DoubanFM
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App : Application
	{
		Mutex mutex;
		object exceptionObject = null;

		public App()
		{
			//只允许运行一个实例
			bool createdNew = false;
			mutex = new Mutex(true, "{DBFE3F28-BA77-4FF6-9EBF-4FED90151A3E}", out createdNew);
			if (!createdNew)
			{
				Channel channel = Channel.FromCommandLineArgs(System.Environment.GetCommandLineArgs().ToList());
				try
				{
					if (channel != null)
					{
						WriteStringToMappedFile(channel.ToCommandLineArgs());
					}
					else
					{
						WriteStringToMappedFile("-show");
					}
				}
				catch { }
				Debug.WriteLine("检测到已有一个豆瓣电台在运行，程序将关闭");
				Shutdown(0);
				return;
			}

			//设置调试输出
			Debug.AutoFlush = true;
			Debug.Listeners.Add(new TextWriterTraceListener("DoubanFM.log"));

			Debug.WriteLine(string.Empty);
			Debug.WriteLine("**********************************************************************");
			Debug.WriteLine("豆瓣电台启动时间：" + App.GetPreciseTime(DateTime.Now));
			Debug.WriteLine("**********************************************************************");
			Debug.WriteLine(string.Empty);

			//出现未处理的异常时，弹出错误报告窗口，让用户发送错误报告
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((sender, e) =>
			{
				if (mutex != null)
				{
					mutex.Close();
					mutex = null;
				}
				if (exceptionObject == null)
				{
					exceptionObject = e.ExceptionObject;
					Debug.WriteLine("**********************************************************************");
					Debug.WriteLine("豆瓣电台出现错误：" + App.GetPreciseTime(DateTime.Now));
					Debug.WriteLine("**********************************************************************");

					try
					{
						StringBuilder sb = new StringBuilder();
						sb.AppendLine(DateTime.Now.ToString());
						sb.AppendLine(ExceptionWindow.GetSystemInformation());
						sb.AppendLine(ExceptionWindow.GetExceptionMessage(exceptionObject));
						
						string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"K.F.Storm\豆瓣电台\error.log");
						string directory = Path.GetDirectoryName(path);
						if (!Directory.Exists(directory))
						{
							Directory.CreateDirectory(directory);
						}
						File.WriteAllText(path, sb.ToString());
					}
					catch { }
					
					Dispatcher.Invoke(new Action(() =>
					{
						try
						{
							DoubanFMWindow mainWindow = MainWindow as DoubanFMWindow;
							if (mainWindow != null)
							{
								Player player = FindResource("Player") as Player;
								if (player != null) player.SaveSettings();
								if (mainWindow._lyricsSetting != null) mainWindow._lyricsSetting.Save();
								if (mainWindow.ShareSetting != null) mainWindow.ShareSetting.Save();
								if (mainWindow.HotKeys != null) mainWindow.HotKeys.Save();
								if (mainWindow.NotifyIcon != null) mainWindow.NotifyIcon.Dispose();
							}
							var window = new ExceptionWindow();
							window.ExceptionObject = exceptionObject;
							window.ShowDialog();
							Process.GetCurrentProcess().Kill();
						}
						catch
						{
							SendReport();
						}
					}));
				}
				else
				{
					SendReport();
				}
			});

			Exit += new ExitEventHandler((sender, e) =>
			{
				if (mutex != null)
				{
					mutex.Close();
					mutex = null;
				}
				Debug.WriteLine(App.GetPreciseTime(DateTime.Now) + " 程序结束，返回代码为" + e.ApplicationExitCode);
			});

			//System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

			/* 这句话可以使Global User Interface这个默认的组合字体按当前系统的区域信息选择合适的字形。
			 * 只对FrameworkElement有效。对于FlowDocument，由于是从FrameworkContentElement继承，
			 * 而且FrameworkContentElement.LanguageProperty.OverrideMetadata()无法再次执行，
			 * 目前我知道的最好的办法是在使用了FlowDocument的XAML的根元素上加上xml:lang="zh-CN"，
			 * 这样就能强制Global User Interface在FlowDocument上使用大陆的字形。
			 * */
			FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name)));
		}

		/// <summary>
		/// 获取时间的一个精确表示
		/// </summary>
		/// <param name="time">时间</param>
		/// <returns>一个精确表示</returns>
		public static string GetPreciseTime(DateTime time)
		{
			return time.ToString() + " " + time.Millisecond + "ms";
		}

		/// <summary>
		/// 内存映射文件的文件名
		/// </summary>
		public static string _mappedFileName = "{04EFCEB4-F10A-403D-9824-1E685C4B7961}";

		/// <summary>
		/// 将字符串写入内存映射文件
		/// </summary>
		internal static void WriteStringToMappedFile(string content)
		{
			using (MemoryMappedFile mappedFile = MemoryMappedFile.OpenExisting(_mappedFileName))
			{
				using (Stream stream = mappedFile.CreateViewStream())
				{
					using (StreamWriter writer = new StreamWriter(stream))
					{
						writer.WriteLine(content);
					}
				}
			}
		}

		/// <summary>
		/// 从内存映射文件中读入字符串
		/// </summary>
		internal static string ReadStringFromMappedFile()
		{
			using (MemoryMappedFile mappedFile = MemoryMappedFile.OpenExisting(_mappedFileName))
			{
				using (Stream stream = mappedFile.CreateViewStream())
				{
					using (StreamReader reader = new StreamReader(stream))
					{
						return reader.ReadLine();
					}
				}
			}
		}
		/// <summary>
		/// 清除内存映射文件
		/// </summary>
		internal static void ClearMappedFile()
		{
			using (MemoryMappedFile mappedFile = MemoryMappedFile.OpenExisting(_mappedFileName))
			{
				using (Stream stream = mappedFile.CreateViewStream())
				{
					using (StreamWriter writer = new StreamWriter(stream))
					{
						writer.WriteLine("");
					}
				}
			}
		}

		private void SendReport()
		{
			if (exceptionObject != null)
			{
				lock (exceptionObject)
				{
					try
					{
						string exceptionMessage = ExceptionWindow.GetExceptionMessage(exceptionObject);
						string userMessage = string.Empty;
						string systemInformation = ExceptionWindow.GetSystemInformation();
#if DEBUG || TEST
						Debug.WriteLine(systemInformation);
						Debug.WriteLine(exceptionMessage);
#else
						ExceptionWindow.SendReport(exceptionMessage, userMessage, systemInformation);
#endif
					}
					catch { }
					Process.GetCurrentProcess().Kill();
				}
			}
		}
	}
}