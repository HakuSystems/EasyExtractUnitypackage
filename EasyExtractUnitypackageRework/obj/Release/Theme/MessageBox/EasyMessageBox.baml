   M S B A M L   `   `   `  ���� !  EasyExtractUnitypackageReworkC    =EasyExtractUnitypackageRework.Theme.MessageBox.EasyMessageBox   W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialdesign1http://materialdesigninxaml.net/winfx/xaml/themes    � Title$  EasyMessageBox��$	��230��$	��506�� � WindowStartupLocation$ CenterScreen=� � WindowStyle$
 None=�$	��230��$	��510�� � 
ResizeMode$ 	CanResize=�    MaterialDesignDarkBackground#���    � Topmost .  � ShowInTaskbar .   � AllowsTransparency . .���  � ColumnDefinitions �� $
��128* ��� $
��127* � � RowDefinitions �� $
��auto ��� $	��51* ��� $	��44* ��� $	��88* �.+��� 	 �� Orientation$	 
Horizontal=�.+��� -   
 ��Name$
 txtTitle�� $  MaterialDesignHeadline6TextBlock#��[ $
��Left=�
�������$��Stretch=�$��20(�$��10q�Message Title�� $��Right=�$	��Top=�$��0��$��1��$��42��$	��201��.+��� -    ��Name$ BtnClose��$��Right=�  MaterialDesignFlatButton#��[ 
���������'  !MaterialDesignThemes.Wpf.PackIcon     Kind$ Close=�
�������� $��2��$��0��$��2��$��1��.+��� $
��Auto=�$��Disabled=�.���� -   $
 
txtMessage��$��5q�$��Actual Mesasage��
�������$
��Wrap=�$��Center=�   MaterialDesignBody1TextBlock#��[  �� TextAlignment$ Center=�� $��3��$��2��$��Bottom=�$��0,20,0,5q� �� $��* ��� $
��auto ��� $
��auto ��� $��* �.+��� -   $ BtnOk��#���  
�������$��1��$��5q�$	��100��$��OK���� -   $ 	BtnCancel��#���  
�������$��2��$��5q�$	��100��$��Cancel���� -   $ BtnYes��#���  
�������$��1��$��5q�$	��100��$	��Yes���� -   $ BtnNo��#���  
�������$��2��$��5q�$	��100��$��No��