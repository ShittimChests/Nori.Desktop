using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Nori.Core.Configuration;
using Nori.Core.Live2D;
using Nori.Core.Logging;
using Nori.Core.Resources;
using Nori.Desktop.Bridge;
using Nori.Desktop.Windows;

namespace Nori.Desktop.Tray;

/// <summary>
/// 系统托盘
///
/// 对应 Rust 版 tray.rs. 托盘是唯一常驻的入口: 左键开主界面, 菜单切换桌宠与退出.
/// </summary>
public static class TrayMenu
{
	/// <summary>已安装的托盘图标; 退出序列里要立刻摘掉, 见 <see cref="Remove"/></summary>
	private static TrayIcon? _installed;

	/// <summary>
	/// 挂上托盘图标与菜单
	/// </summary>
	/// <summary>
	/// 装载托盘图标
	///
	/// 返回是否成功: 部分 Linux 桌面环境没有 StatusNotifier/AppIndicator, 托盘会静默不出现,
	/// 此时把 SupportsTray 置 false, 由前端在主窗内提供常驻入口与退出按钮。
	/// </summary>
	public static bool Install(Application application, AppServices services)
	{
		services.Logger.Write(LogSource.Backend, "info", "初始化托盘菜单");

		NativeMenuItem openMain = new("打开主界面");
		openMain.Click += (_, _) => ShowMain(services);

		NativeMenuItem togglePet = new("显示/隐藏桌宠");
		togglePet.Click += (_, _) =>
		{
			services.Logger.Write(LogSource.Backend, "info", "托盘菜单：切换桌宠显示");
			if (!services.Windows.IsWindowVisible(WindowLabels.Pet) && !CanShowPet(services))
			{
				services.Logger.Write(LogSource.Backend, "warn", "当前 Live2D 模型不可用, 已打开主界面等待重新导入");
				ShowMain(services);
				return;
			}
			services.Windows.TogglePet();
		};

		NativeMenuItem quit = new("退出应用");
		quit.Click += (_, _) =>
		{
			services.Logger.Write(LogSource.Backend, "info", "托盘菜单：退出应用");
			services.Windows.Shutdown();
		};

		TrayIcon tray = new()
		{
			Icon = LoadIcon(),
			ToolTipText = "Nori Desktop Pet - 点击打开主界面",
			Menu = [openMain, togglePet, quit],
		};
		// 左键点击直接开主界面, 不弹菜单
		tray.Clicked += (_, _) => ShowMain(services);

		try
		{
			TrayIcon.SetIcons(application, [tray]);
		}
		catch (Exception exception)
		{
			// 托盘不是必需品: 失败只记日志, 由前端补一个内建入口
			services.Logger.Write(LogSource.Backend, "warn", $"托盘不可用, 将由主界面提供入口: {exception.Message}");
			return false;
		}
		services.Logger.Write(LogSource.Backend, "info", "托盘菜单初始化完成");
		_installed = tray;
		return true;
	}

	/// <summary>
	/// 摘掉托盘图标
	///
	/// 退出序列的第一步, 由 <c>desktop.Exit</c> 调用。托盘是进程级的常驻入口, 而退出清理会
	/// 同步占住 UI 线程数秒 —— 不主动摘掉的话这段时间里点击照旧派发到上面的处理器, 对已经
	/// 销毁的窗口调 Show()。幂等: 重复调用与未安装时都直接返回。
	/// </summary>
	public static void Remove()
	{
		if (_installed is not { } tray) return;
		_installed = null;
		try
		{
			// 先隐藏: 这一步立刻让图标从通知区消失, 不必等 Avalonia 走完自己的清理
			tray.IsVisible = false;
			if (Application.Current is { } application) TrayIcon.SetIcons(application, []);
		}
		catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
		{
			// 退出路径上没有上报渠道; 图标最迟随进程结束由系统回收
		}
	}

	private static bool CanShowPet(AppServices services)
	{
		try
		{
			string? modelId = SupportedModelIds.Normalize(
				services.Config.GetStringOr(ConfigStore.KeySelectedModel, ""));
			return modelId is not null && services.Resources.IsInstalled(ResourceType.Live2D, modelId);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ResourceException)
		{
			return false;
		}
	}

	/// <summary>
	/// 显示主窗口
	/// </summary>
	private static void ShowMain(AppServices services)
	{
		services.Logger.Write(LogSource.Backend, "info", "托盘操作：已显示主窗口");
		services.Windows.Show(WindowLabels.Main);
	}

	/// <summary>
	/// 托盘图标, 缺失时返回 null (托盘会退化成无图标但仍可用)
	/// </summary>
	private static WindowIcon? LoadIcon()
	{
		try
		{
			return new WindowIcon(AssetLoader.Open(new Uri("avares://Nori.Desktop/Assets/icon.ico")));
		}
		catch (Exception exception) when (exception is FileNotFoundException or ArgumentException)
		{
			return null;
		}
	}
}
