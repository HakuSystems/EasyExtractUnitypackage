   M S B A M L   `   `   `  ���� X  TEasyExtractUnitypackageRework, Version=2.0.1.1, Culture=neutral, PublicKeyToken=nullM+clr-namespace:EasyExtractUnitypackageReworkEasyExtractUnitypackageRework  .    (EasyExtractUnitypackageRework.MainWindow   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     =local+clr-namespace:EasyExtractUnitypackageRework     g cMahApps.Metro.IconPacks.Codicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ah dMahApps.Metro.IconPacks.Zondicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137a^ ZMahApps.Metro.IconPacks, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ai eMahApps.Metro.IconPacks.RadixIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137am	 iMahApps.Metro.IconPacks.BootstrapIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137af
 bMahApps.Metro.IconPacks.Microns, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137aj fMahApps.Metro.IconPacks.SimpleIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.EvaIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ai eMahApps.Metro.IconPacks.RPGAwesome, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137am iMahApps.Metro.IconPacks.MaterialDesign, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ae aMahApps.Metro.IconPacks.Modern, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137aj fMahApps.Metro.IconPacks.ForkAwesome, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ah dMahApps.Metro.IconPacks.FileIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137aj fMahApps.Metro.IconPacks.VaadinIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.Ionicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.Fontisto, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ah dMahApps.Metro.IconPacks.Coolicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ac _MahApps.Metro.IconPacks.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137al hMahApps.Metro.IconPacks.PixelartIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.Octicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ai eMahApps.Metro.IconPacks.PicolIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.BoxIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137al hMahApps.Metro.IconPacks.MaterialLight, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137af bMahApps.Metro.IconPacks.Unicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ak gMahApps.Metro.IconPacks.FeatherIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag cMahApps.Metro.IconPacks.Typicons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ae aMahApps.Metro.IconPacks.Entypo, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137aj  fMahApps.Metro.IconPacks.FontAwesome, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag! cMahApps.Metro.IconPacks.Material, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ah" dMahApps.Metro.IconPacks.RemixIcon, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ah# dMahApps.Metro.IconPacks.Fontaudio, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ak$ gMahApps.Metro.IconPacks.WeatherIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ag% cMahApps.Metro.IconPacks.JamIcons, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0c0d510f9915137ayicons-http://metro.mahapps.com/winfx/xaml/iconpacks!     	 
                        ! " # $ % -     � Title$  EasyExtractUnitypackge��$	��450��$	��800�� � WindowStartupLocation$ CenterScreen=� � WindowStyle$
 None=� � AllowsTransparency . 
������ $	��450��$	��800��$��Roboto)� J� Clip ��  �� Rect$ 0,0,800,450� �� RadiusX$ 25Q� �� RadiusY$ 25Q�.��� ���#1B1D1E � ColumnDefinitions �� $��20 ��� �� �� �� �� �� �� �� $��20 �	 � RowDefinitions	 �� $��35 ��� �� �� �� �� �� �� �� $��20 �.+��� 
 �� Orientation$
 
Horizontal=�$��9��.+�� .+��� ���#F63352$
��auto�� J� Effect 3  -System.Windows.Media.Effects.DropShadowEffect     ShadowDepth$ 1Q�   	Direction$ 0Q�   Color$ #1B1D1E��   Opacity$
 0.25Q�   
BlurRadius$ 10Q��� $
 
Horizontal=�.+��� $��10,5,5,5q����� $
 
Horizontal=�.+�. ! (MahApps.Metro.IconPacks.PackIconMaterial  -     Name$ 	CoolEmote��   Kind$ EmoticonCool=�$��15��
��������� -    ��Name$ currentVersion��$��5,0,0,0q�$��currentVersion��
��������� $��5q����� $
 
Horizontal=�.+�  $ Unity=�$��15��
��������� $��5,0,0,0q�$��EasyExtractUnitypackage��
��������� $
 Vertical=�$	��245��.+��� -   $ UpdateStatusTxt��
�������$ ��Running on latest Version!��$
��auto��$��Center=�$��Center=�� -    �Name$ ProgBar��$��Center=�$y�Visible=�$	��245��   EasyProgressBar.style#���   $��10���� -    ��Name$ CheckUpdateBtn��$��5,0,0,0q�$��40��  EasyButton.style#���  ���� $
 
Horizontal=�.+�  -   $ CheckUpdateIcon��$ Update=�
��������� -   $ CheckUpdateTxt��$��Check for updates��$��5,0,0,0q�
��������� -	   $ AbtBtn��$��5,0,0,0q�$��40��#���  ���� $
 
Horizontal=�.+�  $ InformationOutline=�
��������� $��About��$��5,0,0,0q�
��������� -
   $ MinBtn��$��10,0,0,0q�$��40��#���  ���� $
 
Horizontal=�.+�  $ WindowMinimize=�
��������� -   $ CloseBtn��$��	10,0,25,0q�$��40��#���  ���� $
 
Horizontal=�.+�  $ WindowClose=�
������� � -     �Name$ currentPage��$��1��$��1��  � NavigationUIVisibility$ Hidden=�$��7��$��7�� � -   $ currentPage2��$ Hidden=�$��7��$��1��$��8��$��7��