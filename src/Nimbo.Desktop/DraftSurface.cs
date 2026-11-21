using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nimbo.Core;
using System.Text;

namespace Nimbo.Desktop;

public sealed partial class Studio
{
    bool continuousDraft;

    sealed class DraftSurface
    {
        readonly Studio studio;
        readonly Chapter section;
        readonly List<string> endings=[];
        readonly List<TextBox> blocks=[];
        public IReadOnlyList<TextBox> Blocks=>blocks;
        public StackPanel View {get;}=Column(5);
        public TextBox Active {get;private set;}=null!;
        public event Action? Edited;

        public DraftSurface(Studio studio,Chapter section,bool illustrated)
        {
            this.studio=studio;this.section=section;
            var source=section.PlainText;var mark=0;var used=new HashSet<int>();
            if(illustrated){
                foreach(var row in Illustrations.Rows(section)){
                    int start=row[0].Start;
                    AddText(source[mark..start],source[start..row[^1].End],mark);
                    int previous=blocks.Count-1;
                    var strip=ImageRow(row.Count);View.Children.Add(strip);
                    for(int i=0;i<row.Count;i++){
                        var item=row[i];
                        var picture=Picture(item.Image,()=>Offset(previous)+(blocks[previous].Text?.Length??0)+item.Start-start,row.Count>1);
                        Grid.SetColumn(picture,i);strip.Children.Add(picture);used.Add(item.Image);
                    }
                    mark=row[^1].End;
                }
            }
            AddText(source[mark..],"",mark);
            if(illustrated)for(int n=0;n<section.Images.Count;n++)if(!used.Contains(n))View.Children.Add(Picture(n,null,false));
            if(blocks.Count==1)blocks[0].MinHeight=340;
            Active??=blocks[0];
        }

        void AddText(string value,string ending,int start)
        {
            var box=new TextBox{Text=value,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Watermark="Escribe aquí…",
                FontSize=studio.WritingSize,FontFamily=studio.WritingFamily,LineHeight=studio.WritingSize*1.62,
                Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(5,8),MinHeight=46};
            Avalonia.Automation.AutomationProperties.SetName(box,"Texto del manuscrito");
            int position=blocks.Count;blocks.Add(box);endings.Add(ending);View.Children.Add(box);
            if(section.CaretOffset>=start && section.CaretOffset<=start+value.Length){
                box.CaretIndex=section.CaretOffset-start;Active=box;
            }
            box.GotFocus+=(_,_)=>Active=box;
            box.PropertyChanged+=(_,e)=>{
                if(e.Property==TextBox.TextProperty){
                    var text=new StringBuilder();
                    for(int i=0;i<blocks.Count;i++)text.Append(blocks[i].Text).Append(endings[i]);
                    section.PlainText=text.ToString();studio.Changed();Edited?.Invoke();
                }
                if(e.Property==TextBox.CaretIndexProperty && box.IsFocused)section.CaretOffset=Offset(position)+box.CaretIndex;
            };
        }

        int Offset(int block)
        {
            int offset=0;for(int i=0;i<block;i++)offset+=(blocks[i].Text?.Length??0)+endings[i].Length;
            return offset;
        }
        public int Cursor=>Offset(blocks.IndexOf(Active))+Active.CaretIndex;

        Border Picture(int index,Func<int>? markerStart,bool compact)
        {
            string path=Path.Combine(studio.shelf.Media,Path.GetFileName(section.Images[index]));
            var stack=Column(6);var image=Thumbnail(path,900,220);
            void Arrange(){
                if(image==null)return;
                var layout=Illustrations.Layout(section,index);
                image.HorizontalAlignment=layout.EndsWith("izquierda")?HorizontalAlignment.Left:
                    layout.EndsWith("derecha")?HorizontalAlignment.Right:HorizontalAlignment.Center;
                image.Height=compact?124:layout=="Página completa"?340:layout.StartsWith("Ajustar")?148:220;
            }
            if(image!=null){Arrange();stack.Children.Add(image);}else stack.Children.Add(Label("Ilustración no disponible"));
            var bar=new WrapPanel();var caption=Kicker($"Imagen {index+1}");caption.Margin=new Thickness(5,9,12,4);bar.Children.Add(caption);
            var layouts=new[]{"De la sección"}.Concat(ImageLayouts).ToArray();
            var choice=new ComboBox{ItemsSource=layouts,SelectedItem=section.ImageLayouts.GetValueOrDefault(index,"De la sección")};
            ToolTip.SetTip(choice,"Disposición de esta imagen al imprimir");
            choice.SelectionChanged+=(_,_)=>{
                if(choice.SelectedItem is not string selected)return;
                var before=Illustrations.Layout(section,index);
                if(selected=="De la sección")section.ImageLayouts.Remove(index);else section.ImageLayouts[index]=selected;
                Arrange();studio.Changed();
                if(before!=Illustrations.Layout(section,index) && (before==Illustrations.Gallery || Illustrations.Layout(section,index)==Illustrations.Gallery))studio.Editor();
            };bar.Children.Add(choice);
            var menu=new MenuFlyout();
            menu.Items.Add(MenuAction("Mover al cursor de escritura",()=>{
                section.CaretOffset=Illustrations.Move(section,index,markerStart?.Invoke(),Cursor);studio.Changed();studio.Editor();
            }));
            menu.Items.Add(MenuAction("Reemplazar imagen…",async()=>await studio.Guard(async()=>{
                var path=await studio.PickIllustration();if(path==null)return;
                section.Images[index]=studio.shelf.AddImage(path);studio.Changed();studio.Editor();
            })));
            menu.Items.Add(MenuAction("Quitar imagen de la sección",()=>{Illustrations.Remove(section,index);studio.Changed();studio.Editor();}));
            bar.Children.Add(new Button{Content="Imagen  ▾",Flyout=menu});
            var legend=new TextBox{Watermark="Pie de imagen",Text=section.ImageCaptions.GetValueOrDefault(index,""),TextWrapping=TextWrapping.Wrap};
            legend.TextChanged+=(_,_)=>{string value=legend.Text??"";if(value==section.ImageCaptions.GetValueOrDefault(index,""))return;
                if(value=="")section.ImageCaptions.Remove(index);else section.ImageCaptions[index]=value;studio.Changed();};
            if(compact)stack.Children.Add(new Expander{Header=$"Imagen {index+1}",Content=bar});else stack.Children.Add(bar);
            stack.Children.Add(legend);
            return new Border{Child=stack,Background=studio.ImageSurface,CornerRadius=new CornerRadius(6),Padding=new Thickness(compact?6:12,8),Margin=new Thickness(0,4)};
        }
    }
    IBrush ImageSurface=>EditionPalette.Mix(palette.Wood,IsLight?"#FFFFFF":"#292728",IsLight?.84:.58);
    static Grid ImageRow(int count)=>new(){ColumnDefinitions=new ColumnDefinitions(string.Join(",",Enumerable.Repeat("*",count))),ColumnSpacing=6};
    async Task<string[]> PickIllustrations(bool multiple)
    {
        var picks=await StorageProvider.OpenFilePickerAsync(new(){Title=multiple?"Elegir ilustraciones":"Elegir ilustración",AllowMultiple=multiple,
            FileTypeFilter=[new FilePickerFileType("Imágenes"){Patterns=["*.jpg","*.jpeg","*.png"]}]});
        return picks.Select(p=>p.TryGetLocalPath()).OfType<string>().ToArray();
    }
    async Task<string?> PickIllustration()
    {
        return (await PickIllustrations(false)).FirstOrDefault();
    }
}
