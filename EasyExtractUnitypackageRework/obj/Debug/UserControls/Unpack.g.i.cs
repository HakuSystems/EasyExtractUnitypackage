﻿#pragma checksum "..\..\..\UserControls\Unpack.xaml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "C39239F643258C883EA6D792AF4FA560389E1AA1527381C4FB2C8FC99F3CDC9C"
//------------------------------------------------------------------------------
// <auto-generated>
//     Dieser Code wurde von einem Tool generiert.
//     Laufzeitversion:4.0.30319.42000
//
//     Änderungen an dieser Datei können falsches Verhalten verursachen und gehen verloren, wenn
//     der Code erneut generiert wird.
// </auto-generated>
//------------------------------------------------------------------------------

using EasyExtractUnitypackageRework.UserControls;
using MahApps.Metro.IconPacks;
using MahApps.Metro.IconPacks.Converter;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace EasyExtractUnitypackageRework.UserControls {
    
    
    /// <summary>
    /// Unpack
    /// </summary>
    public partial class Unpack : System.Windows.Controls.UserControl, System.Windows.Markup.IComponentConnector {
        
        
        #line 44 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal MahApps.Metro.IconPacks.PackIconMaterial DragDropIcon;
        
        #line default
        #line hidden
        
        
        #line 57 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Border DirOutputBar;
        
        #line default
        #line hidden
        
        
        #line 63 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button DirOutputName;
        
        #line default
        #line hidden
        
        
        #line 78 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBlock tFilesV;
        
        #line default
        #line hidden
        
        
        #line 80 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBlock uFilesV;
        
        #line default
        #line hidden
        
        
        #line 90 "..\..\..\UserControls\Unpack.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button SearchInsBtn;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/EasyExtractUnitypackageRework;component/usercontrols/unpack.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\UserControls\Unpack.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            
            #line 9 "..\..\..\UserControls\Unpack.xaml"
            ((EasyExtractUnitypackageRework.UserControls.Unpack)(target)).DragEnter += new System.Windows.DragEventHandler(this.UserControl_DragEnter);
            
            #line default
            #line hidden
            
            #line 10 "..\..\..\UserControls\Unpack.xaml"
            ((EasyExtractUnitypackageRework.UserControls.Unpack)(target)).DragLeave += new System.Windows.DragEventHandler(this.UserControl_DragLeave);
            
            #line default
            #line hidden
            
            #line 11 "..\..\..\UserControls\Unpack.xaml"
            ((EasyExtractUnitypackageRework.UserControls.Unpack)(target)).Drop += new System.Windows.DragEventHandler(this.UserControl_Drop);
            
            #line default
            #line hidden
            
            #line 14 "..\..\..\UserControls\Unpack.xaml"
            ((EasyExtractUnitypackageRework.UserControls.Unpack)(target)).Loaded += new System.Windows.RoutedEventHandler(this.UserControl_Loaded);
            
            #line default
            #line hidden
            return;
            case 2:
            this.DragDropIcon = ((MahApps.Metro.IconPacks.PackIconMaterial)(target));
            return;
            case 3:
            this.DirOutputBar = ((System.Windows.Controls.Border)(target));
            return;
            case 4:
            this.DirOutputName = ((System.Windows.Controls.Button)(target));
            
            #line 63 "..\..\..\UserControls\Unpack.xaml"
            this.DirOutputName.Click += new System.Windows.RoutedEventHandler(this.DirOutputName_Click);
            
            #line default
            #line hidden
            return;
            case 5:
            this.tFilesV = ((System.Windows.Controls.TextBlock)(target));
            return;
            case 6:
            this.uFilesV = ((System.Windows.Controls.TextBlock)(target));
            return;
            case 7:
            this.SearchInsBtn = ((System.Windows.Controls.Button)(target));
            
            #line 91 "..\..\..\UserControls\Unpack.xaml"
            this.SearchInsBtn.Click += new System.Windows.RoutedEventHandler(this.SearchInsBtn_Click);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

