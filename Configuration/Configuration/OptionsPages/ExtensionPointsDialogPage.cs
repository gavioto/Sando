﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace Configuration.OptionsPages
{
	[Guid("B0002DC2-56EE-4931-93F7-70D6E9863940")]
	public class SandoDialogPage : DialogPage
	{
		#region Properties
		/// <summary> 
		/// Gets the window an instance of DialogPage that it uses as its user interface. 
		/// </summary> 
		/// <devdoc> 
		/// The window this dialog page will use for its UI. 
		/// This window handle must be constant, so if you are 
		/// returning a Windows Forms control you must make sure 
		/// it does not recreate its handle.  If the window object 
		/// implements IComponent it will be sited by the  
		/// dialog page so it can get access to global services. 
		/// </devdoc> 
		[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		protected override IWin32Window Window
		{
			get
			{
				SandoOptionsControl optionsControl = new SandoOptionsControl();
				optionsControl.Location = new Point(0, 0);
				optionsControl.OptionsPage = this;
				optionsControl.ExtensionPointsPluginDirectoryPath = this.ExtensionPointsPluginDirectoryPath;
				return optionsControl;
			}
		}
		/// <summary> 
		/// Gets or sets the path to the image file. 
		/// </summary> 
		/// <remarks>The property that needs to be persisted.</remarks> 
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public string ExtensionPointsPluginDirectoryPath { get; set; }
		#endregion Properties 
	}
}
