﻿#pragma checksum "..\..\..\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "CCFA6ABC5D589B84CDC4C6B99EB842B4B2AB7D65"
//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

using BinanceFuturesTrader.Converters;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Converters;
using MaterialDesignThemes.Wpf.Transitions;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
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


namespace BinanceFuturesTrader {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        
        #line 62 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ComboBox AccountComboBox;
        
        #line default
        #line hidden
        
        
        #line 956 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal MaterialDesignThemes.Wpf.Card ConditionalOrderCard;
        
        #line default
        #line hidden
        
        
        #line 962 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button AddPositionBtn;
        
        #line default
        #line hidden
        
        
        #line 966 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button ClosePositionBtn;
        
        #line default
        #line hidden
        
        
        #line 973 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid AddPositionPanel;
        
        #line default
        #line hidden
        
        
        #line 981 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid NoPositionGrid;
        
        #line default
        #line hidden
        
        
        #line 1053 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid HasPositionGrid;
        
        #line default
        #line hidden
        
        
        #line 1128 "..\..\..\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid ClosePositionPanel;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.3.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/BinanceFuturesTrader;V1.0.0.0;component/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.3.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.AccountComboBox = ((System.Windows.Controls.ComboBox)(target));
            return;
            case 2:
            
            #line 202 "..\..\..\MainWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.TestMarketValue_Click);
            
            #line default
            #line hidden
            return;
            case 3:
            
            #line 875 "..\..\..\MainWindow.xaml"
            ((System.Windows.Controls.TextBox)(target)).MouseEnter += new System.Windows.Input.MouseEventHandler(this.RiskCapitalTextBox_MouseEnter);
            
            #line default
            #line hidden
            
            #line 876 "..\..\..\MainWindow.xaml"
            ((System.Windows.Controls.TextBox)(target)).MouseLeave += new System.Windows.Input.MouseEventHandler(this.RiskCapitalTextBox_MouseLeave);
            
            #line default
            #line hidden
            return;
            case 4:
            
            #line 887 "..\..\..\MainWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.ToggleConditionalOrder_Click);
            
            #line default
            #line hidden
            return;
            case 5:
            this.ConditionalOrderCard = ((MaterialDesignThemes.Wpf.Card)(target));
            return;
            case 6:
            this.AddPositionBtn = ((System.Windows.Controls.Button)(target));
            
            #line 963 "..\..\..\MainWindow.xaml"
            this.AddPositionBtn.Click += new System.Windows.RoutedEventHandler(this.AddPositionConditional_Click);
            
            #line default
            #line hidden
            return;
            case 7:
            this.ClosePositionBtn = ((System.Windows.Controls.Button)(target));
            
            #line 967 "..\..\..\MainWindow.xaml"
            this.ClosePositionBtn.Click += new System.Windows.RoutedEventHandler(this.ClosePositionConditional_Click);
            
            #line default
            #line hidden
            return;
            case 8:
            this.AddPositionPanel = ((System.Windows.Controls.Grid)(target));
            return;
            case 9:
            this.NoPositionGrid = ((System.Windows.Controls.Grid)(target));
            return;
            case 10:
            this.HasPositionGrid = ((System.Windows.Controls.Grid)(target));
            return;
            case 11:
            this.ClosePositionPanel = ((System.Windows.Controls.Grid)(target));
            return;
            }
            this._contentLoaded = true;
        }
    }
}

