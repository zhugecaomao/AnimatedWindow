using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Diagnostics;
using System.Runtime .InteropServices;
using Microsoft.Win32;

namespace AnimatedWindow
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public sealed class AnimatedWindowForm : System.Windows.Forms.Form
	{
		private static MemoryMappedFile sharedMemory;
		private bool AnimationDisabled = false;
		private bool systemShutdown = false;
		private System.ComponentModel.IContainer components;
		private const  int WM_QUERYENDSESSION = 0x11;
		private const int IDANI_CAPTION = 3;
		private const int SW_HIDE = 0;
		private const int SW_MAX = 10;
		private const int SW_MAXIMIZE = 3;
		private const int SW_MINIMIZE = 6;
		private const int SW_NORMAL = 1;
		private const int SW_RESTORE = 9;
		private const int SW_SHOW = 5;
		private const int SW_SHOWDEFAULT = 10;
		private const int SW_SHOWMAXIMIZED = 3;
		private const int SW_SHOWMINIMIZED = 2;
		private const int SW_SHOWMINNOACTIVE = 7;
		private const int SW_SHOWNA = 8;
		private const int SW_SHOWNOACTIVATE = 4;
		private const int SW_SHOWNORMAL = 1;

		private System.Windows.Forms.Button exitButton;
		private System.Windows.Forms.Label minimizeLabel;
		private System.Windows.Forms.NotifyIcon notifyIcon;


		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
			public override string ToString()
			{
				return ("Left :"+left.ToString ()+","+"Top :"+top.ToString()+","+"Right :"+right.ToString ()+","+"Bottom :"+bottom.ToString ());
			}
		}

		[DllImport("User32.dll",EntryPoint="ShowWindow",CharSet=CharSet.Auto)]
		private static extern bool ShowWindow(IntPtr hWnd,int nCmdShow);
		
		[DllImport("User32.dll",EntryPoint="UpdateWindow",CharSet=CharSet.Auto)]
		private static extern bool UpdateWindow(IntPtr hWnd);

		[DllImport("user32.dll", EntryPoint="IsIconic",CharSet=CharSet.Auto)]
		private static extern bool IsIconic(IntPtr hWnd);

		[DllImport("user32.dll", EntryPoint="FindWindowEx",CharSet=CharSet.Auto)]
		private extern static IntPtr FindWindowEx(
			IntPtr hwndParent, 
			IntPtr hwndChildAfter,
			[MarshalAs(UnmanagedType.LPTStr)]
			string lpszClass,
			[MarshalAs(UnmanagedType.LPTStr)]
			string lpszWindow);

		[DllImport("user32.dll", EntryPoint="DrawAnimatedRects",CharSet=CharSet.Auto)]
		private static extern bool DrawAnimatedRects(IntPtr hwnd, int idAni, ref RECT lprcFrom, ref RECT lprcTo);

		[DllImport("user32.dll",  EntryPoint="GetWindowRect",CharSet=CharSet.Auto)]
		private extern static bool GetWindowRect(IntPtr hwnd, ref RECT lpRect);
		
		public AnimatedWindowForm()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			// A user can enable/disable window animation by setting the the "MinAnimate" key under 
			// HKeyCurrentUser\Control Panel\Desktop. This value need to be read inorder to set our Animation Falg.
			RegistryKey animationKey = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop\\WindowMetrics",true);
			object animKeyValue = animationKey.GetValue("MinAnimate");
			
			if(System.Convert.ToInt32 (animKeyValue.ToString()) == 0)
				this.AnimationDisabled = true;
			else
				this.AnimationDisabled = false;
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				this.notifyIcon .Visible = false;
				this.notifyIcon .Icon .Dispose ();
				this.notifyIcon.Dispose(); 

				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(AnimatedWindowForm));
			this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
			this.exitButton = new System.Windows.Forms.Button();
			this.minimizeLabel = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// notifyIcon
			// 
			this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
			this.notifyIcon.Text = "";
			this.notifyIcon.Visible = true;
			
			this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
			// 
			// exitButton
			// 
			this.exitButton.Location = new System.Drawing.Point(136, 136);
			this.exitButton.Name = "exitButton";
			this.exitButton.Size = new System.Drawing.Size(48, 23);
			this.exitButton.TabIndex = 0;
			this.exitButton.Text = "Exit";
			this.exitButton.Click += new System.EventHandler(this.exitButton_Click);
			// 
			// minimizeLabel
			// 
			this.minimizeLabel.BackColor = System.Drawing.Color.White;
			this.minimizeLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.minimizeLabel.Location = new System.Drawing.Point(68, 32);
			this.minimizeLabel.Name = "minimizeLabel";
			this.minimizeLabel.Size = new System.Drawing.Size(184, 88);
			this.minimizeLabel.TabIndex = 1;
			this.minimizeLabel.Text = "This window minimizes to the system tray when closed.";
			// 
			// AnimatedWindowForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(320, 190);
			this.Controls.Add(this.minimizeLabel);
			this.Controls.Add(this.exitButton);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "AnimatedWindowForm";
			this.Text = "AnimatedWindow";
			this.Load += new System.EventHandler(this.AnimatedWindowForm_Load);
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			// Used to check if we can create a new mutex
			bool newMutexCreated = false;
			// The name of the mutex is to be prefixed with Local\ to make sure that its is created in the per-session namespace, not in the global namespace.
			string mutexName = "Local\\" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			
			Mutex mutex = null;
			try
			{
				// Create a new mutex object with a unique name
				mutex = new Mutex(false, mutexName, out newMutexCreated);
			}
			catch(Exception ex)
			{
				MessageBox.Show (ex.Message+"\n\n"+ex.StackTrace+"\n\n"+"Application Exiting...","Exception thrown");	
				Application.Exit ();
			}
				
			// When the mutex is created for the first time we run the program since it is the first instance.
			if(newMutexCreated)
			{
				//Create the Shared Memory to store the window handle. This memory is shared between processes
				lock(typeof(AnimatedWindowForm))
				{
					sharedMemory = MemoryMappedFile.CreateMMF("Local\\sharedMemoryAnimatedWindow",MemoryMappedFile.FileAccess .ReadWrite ,8);
				}
				Application.Run(new AnimatedWindowForm());
			}
			
			else// If the mutex already exists, no need to launch a new instance of the program because a previous instance is running .
			{
				try
				{
					// Get the Program's main window handle, which was previously stored in shared memory.
					IntPtr mainWindowHandle = System.IntPtr.Zero;
					lock(typeof(AnimatedWindowForm))
					{
						mainWindowHandle = MemoryMappedFile.ReadHandle("Local\\sharedMemoryAnimatedWindow");
					}
					if(mainWindowHandle != IntPtr.Zero)
					{
						if(IsIconic(mainWindowHandle))
							ShowWindow(mainWindowHandle,SW_SHOWNORMAL); // Restore the Window 
						else
							ShowWindow(mainWindowHandle,SW_RESTORE); // Restore the Window 

						UpdateWindow(mainWindowHandle);
					}
					return;
				}
				catch(Exception ex)
				{
					MessageBox.Show (ex.Message+"\n\n"+ex.StackTrace+"\n\n"+"Application Exiting...","Exception thrown");	
				}

				// Tell the garbage collector to keep the Mutex alive until the code execution reaches this point, ie. normally when the program is exiting.
				GC.KeepAlive(mutex);
				// Release the Mutex 
				try
				{
					mutex.ReleaseMutex();
				}
				catch(ApplicationException ex)
				{
					MessageBox.Show (ex.Message+"\n\n"+ex.StackTrace,"Exception thrown");	
					GC.Collect();
				}
			}
		}

		private void AnimatedWindowForm_Load(object sender, System.EventArgs e)
		{
			this.ShowInTaskbar = true;
			
		}
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated (e);
			IntPtr mainWindowHandle = this.Handle;
			try
			{
				lock(this)
				{
					//Write the handle to the Shared Memory 
					sharedMemory.WriteHandle (mainWindowHandle);
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show (ex.Message+"\n\n"+ex.StackTrace+"\n\n"+"Application Exiting...","Exception thrown");
				Application.Exit();
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			try
			{
				if(systemShutdown == true)
					e.Cancel = false;
				else
				{
					e.Cancel = true;
					this.AnimateWindow();
					this.Visible = false;
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show (ex.Message+"\n\n"+ex.StackTrace+"\n\n"+"Application Exiting...","Exception thrown");
			}

			base.OnClosing (e);
		}

		private void AnimateWindow()
		{
			// if the user has not disabled animating windows...
			if(!this.AnimationDisabled)
			{
				RECT animateFrom = new RECT();
				GetWindowRect(this.Handle, ref animateFrom);

				RECT animateTo = new RECT ();
				IntPtr notifyAreaHandle = GetNotificationAreaHandle();

				if (notifyAreaHandle != IntPtr.Zero)
				{
					if ( GetWindowRect(notifyAreaHandle, ref animateTo) == true)
					{
						DrawAnimatedRects(this.Handle,IDANI_CAPTION,ref animateFrom,ref animateTo);
					}
				}
			}
		}
		private void notifyIcon_DoubleClick(object sender, System.EventArgs e)
		{
			if(this.Visible == false)
			{
				this.Activate ();
				this.Visible = true;
			}
			if(this.WindowState == FormWindowState.Minimized)
				this.WindowState = FormWindowState.Normal;
		}

		protected override void OnClosed(EventArgs e)
		{
			this.notifyIcon.Visible = false;
			this.notifyIcon.Icon .Dispose ();
			this.notifyIcon.Dispose(); 
			base.OnClosed (e);

		}
		protected override void WndProc(ref Message m)
		{
			// Once the program recieves WM_QUERYENDSESSION message, set the boolean systemShutdown.
			
			if (m.Msg == WM_QUERYENDSESSION)
				systemShutdown = true;
			base.WndProc(ref m);
		}

		private IntPtr GetNotificationAreaHandle()
		{
			IntPtr hwnd = FindWindowEx(IntPtr.Zero,IntPtr.Zero,"Shell_TrayWnd",null);
			Console.WriteLine ("Shell_TrayWnd"+hwnd.ToString());
			hwnd = FindWindowEx(hwnd , IntPtr.Zero ,"TrayNotifyWnd",null);
			Console.WriteLine ("TrayNotifyWnd"+hwnd.ToString());
			hwnd = FindWindowEx(hwnd , IntPtr.Zero ,"SysPager",null);
			Console.WriteLine ("SysPager"+hwnd.ToString());

			if (hwnd != IntPtr.Zero)
				hwnd = FindWindowEx(hwnd , IntPtr.Zero ,null,"Notification Area");

			Console.WriteLine ("Notification Area"+hwnd.ToString());
			return hwnd;		
		}

		private void exitButton_Click(object sender, System.EventArgs e)
		{
			Application.Exit();
		}

		
		
	}
}
