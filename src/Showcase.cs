using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace InzoneH9Widget
{
    public sealed class DataError : Exception { public DataError(string text) : base(text) {} }
    public sealed class Position
    {
        public double left = 760, top = 280;
        public bool desktop;
    }
    public sealed class Showcase : Window
    {
        static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "H9BatteryShowcase");
        readonly DispatcherTimer poll = new DispatcherTimer(), clock = new DispatcherTimer();
        readonly TextBlock percent, state, detail, stamp;
        readonly ProgressBar bar;
        readonly Button refresh;
        readonly string capture;
        Reading reading = new Reading();
        Position position = new Position();
        DesktopLayer layer;
        Point? dragStart;
        Point windowStart;
        bool busy, closing;

        public Showcase(string capture)
        {
            this.capture = capture;
            Title = "INZONE H9 电量 · 独立展示版";
            Width = 354; Height = 240; Opacity = .85;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            try { position = new JavaScriptSerializer().Deserialize<Position>(File.ReadAllText(Path.Combine(DataFolder,"position.json"))) ?? new Position(); } catch {}
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, Math.Min(position.left, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(position.top, area.Bottom - Height));
            Content = XamlReader.Parse(Layout);
            percent = Find<TextBlock>("Percent"); state = Find<TextBlock>("State");
            detail = Find<TextBlock>("Detail"); stamp = Find<TextBlock>("Stamp");
            bar = Find<ProgressBar>("Bar"); refresh = Find<Button>("Refresh");
            refresh.Click += async delegate { await Refresh(); };
            Find<Button>("Close").Click += delegate { Close(); };
            Find<Border>("DragHandle").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                if (layer == null) return;
                if (!layer.IsDesktop) { DragMove(); SavePosition(); e.Handled = true; return; }
                dragStart = PointToScreen(e.GetPosition(this));
                DesktopLayer.Rect rect; DesktopLayer.GetWindowRect(layer.Handle, out rect);
                windowStart = new Point(rect.Left,rect.Top);
                if (!CaptureMouse()) dragStart = null;
                e.Handled = true;
            };
            MouseMove += delegate(object sender, MouseEventArgs e) {
                if (dragStart.HasValue && e.LeftButton == MouseButtonState.Pressed)
                    layer.MoveToScreen(windowStart + (PointToScreen(e.GetPosition(this)) - dragStart.Value));
            };
            MouseLeftButtonUp += delegate { if(dragStart.HasValue) { dragStart=null; ReleaseMouseCapture(); SavePosition(); } };
            LostMouseCapture += delegate { dragStart=null; };
            var menu = new ContextMenu();
            var mode = new MenuItem { Header = "切换桌面／普通窗口" };
            mode.Click += delegate { try { layer.SetDesktop(!layer.IsDesktop); position.desktop=layer.IsDesktop; SavePosition(); } catch(Exception e) { MessageBox.Show(this,e.Message,Title); } };
            menu.Items.Add(mode); ContextMenu = menu;
            poll.Interval = TimeSpan.FromSeconds(60); poll.Tick += async delegate { await Refresh(); };
            clock.Interval = TimeSpan.FromSeconds(1); clock.Tick += delegate { Render(); };
            Loaded += async delegate {
                layer = new DesktopLayer(this);
                if(position.desktop) { try { layer.SetDesktop(true); } catch { position.desktop=false; } }
                poll.Start(); clock.Start(); await Refresh();
                if(capture != null) { Capture(capture); Close(); }
            };
            Closing += delegate { closing=true; poll.Stop(); clock.Stop(); SavePosition(); };
        }
        T Find<T>(string name) where T:class { return ((FrameworkElement)Content).FindName(name) as T; }
        void SavePosition()
        {
            position.left=Left; position.top=Top;
            try { Save("position.json",position); } catch {}
        }
        static void Save(string name,object value)
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(Path.Combine(DataFolder,name),new JavaScriptSerializer().Serialize(value));
        }
        async Task Refresh()
        {
            if(busy || closing) return;
            busy=true; refresh.IsEnabled=false; Render();
            try {
                var result=await Task.Run(() => Reader.Read());
                // The existing dashboard keeps running. Wait briefly if its own
                // short hardware query currently owns the same receiver port.
                if(result.status == "busy") { await Task.Delay(5000); if(closing) return; result=await Task.Run(() => Reader.Read()); }
                if(!closing) { reading=result; try { Save("status.json",reading); } catch {} }
            } finally { busy=false; if(!closing) { refresh.IsEnabled=true; Render(); } }
        }
        void Render()
        {
            DateTimeOffset updated;
            bool valid=reading.status=="ok" && reading.battery.HasValue;
            bool fresh=valid && DateTimeOffset.TryParse(reading.updatedAt,out updated) && DateTimeOffset.Now-updated < TimeSpan.FromSeconds(90);
            percent.Text=fresh ? reading.battery.Value+"%" : "—";
            bar.Value=fresh ? reading.battery.Value : 0; bar.Opacity=fresh ? 1 : .35;
            var color=(Brush)new BrushConverter().ConvertFromString(fresh && reading.battery<=20 ? "#BD754E" : "#8A65B7");
            percent.Foreground=bar.Foreground=color;
            state.Text=fresh ? (reading.charging==true ? "正在充电" : reading.charging==false ? "使用电池" : "状态未知") : busy ? "读取中" : "暂无读数";
            detail.Text=busy ? "正在读取耳机接收器…" : valid && !fresh ? "读数已过期，等待重新读取。" : valid ? "耳机剩余电量" : reading.message;
            detail.ToolTip=detail.Text;
            stamp.Text=fresh && DateTimeOffset.TryParse(reading.updatedAt,out updated) ? "更新 "+updated.ToLocalTime().ToString("HH:mm:ss") : "每 60 秒自动重试";
        }
        void Capture(string path)
        {
            UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Width*2,(int)Height*2,192,192,PixelFormats.Pbgra32); bitmap.Render((Visual)Content);
            var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var file=File.Create(path)) png.Save(file);
        }
        const string Layout=@"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Margin='11'>
 <Grid.Resources>
  <Style TargetType='Button'><Setter Property='Cursor' Value='Hand'/><Setter Property='Foreground' Value='#8A8493'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='8' Padding='8,5'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#E7ECF2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='ProgressBar'><Setter Property='Height' Value='5'/><Setter Property='Maximum' Value='100'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ProgressBar'><Grid><Border Background='{TemplateBinding Background}' CornerRadius='3'/><Grid x:Name='PART_Track'><Border x:Name='PART_Indicator' HorizontalAlignment='Left' Background='{TemplateBinding Foreground}' CornerRadius='3'/></Grid></Grid></ControlTemplate></Setter.Value></Setter></Style>
 </Grid.Resources>
 <Border Background='#FCFCFD' CornerRadius='24' BorderBrush='#E4E6EF' BorderThickness='1'>
  <Border.Effect><DropShadowEffect Color='#293852' BlurRadius='18' ShadowDepth='3' Opacity='.18'/></Border.Effect>
  <Grid Margin='22,18,22,18'>
   <Grid.RowDefinitions><RowDefinition Height='30'/><RowDefinition Height='66'/><RowDefinition Height='6'/><RowDefinition Height='48'/><RowDefinition Height='*'/></Grid.RowDefinitions>
   <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='22'/><ColumnDefinition Width='*'/><ColumnDefinition Width='30'/><ColumnDefinition Width='26'/></Grid.ColumnDefinitions>
    <Border x:Name='DragHandle' Background='Transparent' Cursor='SizeAll' ToolTip='拖动书签移动组件'><Path Width='10' Height='16' Stretch='Fill' Fill='#B88362' Data='M 1,1 L 13,1 L 13,19 L 7,15 L 1,19 Z'/></Border>
    <StackPanel Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'><Path Width='15' Height='15' Stretch='Uniform' Stroke='#8A65B7' StrokeThickness='1.7' Data='M 2,10 L 2,7 C 2,0 14,0 14,7 L 14,14 L 11,14 L 11,8 L 14,8 M 2,8 L 5,8 L 5,14 L 2,14 Z'/><TextBlock Text='INZONE H9' Foreground='#51475F' FontSize='13' FontWeight='SemiBold' Margin='8,0,0,0'/></StackPanel>
    <Button x:Name='Refresh' Grid.Column='2' Content='↻' FontSize='15' Background='#F0ECF5' Height='28' ToolTip='刷新耳机电量'/><Button x:Name='Close' Grid.Column='3' Content='×' FontSize='17' Background='Transparent' Height='26' ToolTip='关闭展示版'/>
   </Grid>
   <Grid Grid.Row='1'><TextBlock x:Name='Percent' Text='—' FontSize='40' FontWeight='SemiBold' Foreground='#8A65B7' VerticalAlignment='Center'/><Border Background='#F1EDF7' CornerRadius='8' Padding='10,5' HorizontalAlignment='Right' VerticalAlignment='Center'><TextBlock x:Name='State' Text='读取中' FontSize='10' Foreground='#8A65B7'/></Border></Grid>
   <ProgressBar Grid.Row='2' x:Name='Bar' Foreground='#8A65B7' Background='#E9E4F0'/>
   <TextBlock Grid.Row='3' x:Name='Detail' FontSize='10' Foreground='#8A8493' TextWrapping='Wrap' VerticalAlignment='Center' MaxHeight='45'/>
   <Grid Grid.Row='4' VerticalAlignment='Bottom'><TextBlock Text='USB 接收器 · 自动更新' FontSize='9' Foreground='#9B95A4'/><TextBlock x:Name='Stamp' FontSize='9' Foreground='#9B95A4' HorizontalAlignment='Right'/></Grid>
  </Grid>
 </Border>
</Grid>";
    }
    public static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            bool created;
            using(var mutex=new Mutex(true,"Local\\H9BatteryShowcase",out created))
            {
                if(!created) return 0;
                try { return new Application().Run(new Showcase(args.Length==2 && args[0]=="--capture" ? args[1] : null)); }
                catch(Exception e) { MessageBox.Show("展示版启动失败（"+e.GetType().Name+"）。","INZONE H9 电量"); return 1; }
            }
        }
    }
}
