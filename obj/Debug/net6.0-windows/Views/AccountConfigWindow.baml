   M S B A M L   `   `   `  ����  BinanceFuturesTrader4    .BinanceFuturesTrader.Views.AccountConfigWindow   5   	   W SPresentationCore, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  5   	     � Title$  账户配置��5   	   $	��500��6   $	��400��6#    � WindowStartupLocation$ CenterOwner=�5   	    � 
ResizeMode$ NoResize=�5   	   .��5	      � $��20q�6    � RowDefinitions 5
   
   �� 5      $
��Auto �6   �� 5      $��* �6   �� 5      $
��Auto �6   .+�5   
   �� $��0��6   $��账户配置��6!   $��18(�6-   $
��Bold"�6;   $��Center=�5      $��0,0,0,20q�60   �� 5   
   $��1��6   .+�5      �� )  #MaterialDesignThemes.Wpf.HintAssist   Hint$ 账户名称��6   $��0,0,0,15q�5      ��5      �� *AccountName+{�5      �� *IsNewAccount+5      �� 5      $ API Key��6   $��0,0,0,15q�5      ��5      �� *ApiKey+5      G� 5      -   5         G�Name$ SecretKeyBox��5      $ 
Secret Key��5      $��0,0,0,15q�5       �� 5"      $ 风险金次数��6   $��0,0,0,15q�5$      ��5#      �� *RiskCapitalTimes+5$      �� 5&      $��测试网络��6   $��0,0,0,15q�5(       d� 	IsChecked 5'      �� *	IsTestNet+5(      �� 5*      $��	说明：��6   $
��Bold"�6#   $��0,10,0,0q�65   �� 5+      $
��Wrap=�6   $��12(�6,   
�������ge• 风险金次数：用于计算可用风险金，可用风险金 = 账户权益 ÷ 风险金次数�� 5,   8   *(• API密钥需要有期货交易权限�� 5-   "   ?=• 测试网络用于模拟交易，不会产生实际费用5/      50      �� 52   
   $��2��6    �� Orientation$ 
Horizontal=�6"   $��Right=�53      $��0,20,0,0q�60   .+�54      �� $��取消��6       MaterialDesignOutlinedButton#��[  55      $��0,0,10,0q�56      ��54   "   �� *CancelCommand+56      �� 57      $��保存��6     MaterialDesignRaisedButton#��[ 59      ��57   "   �� *SaveCommand+��58      ��  �� ElementName$ SecretKeyBox��59      5:      5;      5<      