using System;
using System.Drawing;
using System.Windows.Forms;

namespace TeconMoon_s_WiiVC_Injector
{
    namespace Utils
    {
        namespace WinForms
        {
            internal class SplitButton : Button
            {
                public SplitButton()
                {
                    //
                    // McAfee(lovesafe 16R22, up-to-date, 5/28/2020) 
                    // will kill us because the following line:
                    //
                    // ImageAlign = ContentAlignment.MiddleRight;
                    //
                    // Indeed, you can't set any button's ImageAlign property
                    // in THIS project. I have tested this class in a new emtpy 
                    // winform project, and it's for free to use ImageAlign.
                    //
                    typeof(SplitButton).GetProperty("ImageAlign")
                        .SetValue(this, ContentAlignment.MiddleRight);

                    //
                    // But the magic is that after the reflection method used,
                    // here you can call ImageAlign = ContentAlignment.MiddleRight
                    // without any problem now. And it's not the problem of the
                    // code order. If the reflection method was not called before
                    // it, we will still be killed even if you put it to 
                    // last line of the function. 
                    // So what the hell is in McAfee's virus signature database...
                    //
                    // ImageAlign = ContentAlignment.MiddleRight;

                    TextImageRelation = TextImageRelation.TextBeforeImage;

                    //
                    // Assign slpit image.
                    //
                    Image = CreateSplitImage();

                    splitWidth = SplitWidth;
                    marginTop = MarginTop;
                    marginBottom = MarginBottom;
                }

                //
                // Create a triangle image.
                //
                private static Image CreateSplitImage()
                {
                    int splitImgWidth = 8;
                    int splitImgHeight = 4;
                    Bitmap bitmap = new Bitmap(splitImgWidth, splitImgHeight);
                    Point[] points = {
                        new Point(0, 0),
                        new Point(splitImgWidth, 0),
                        new Point(splitImgWidth / 2, splitImgHeight) };

                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.FillPolygon(
                            new SolidBrush(Color.Black),
                            points);
                    }

                    return bitmap;
                }

                //
                // With the reflection method, using this property will casue
                // a performance issue.
                //
                private int SplitWidth
                {
                    get
                    {
                        dynamic margin = typeof(SplitButton).GetProperty("Margin").GetValue(this);
                        return Image.Width + margin.Left + margin.Right;
                    }
                }

                //
                // With the reflection method, using this property will casue
                // a performance issue.
                //
                private int MarginTop
                {
                    get
                    {
                        dynamic margin = typeof(SplitButton).GetProperty("Margin").GetValue(this);
                        return margin.Top;
                    }
                }

                //
                // Can't get bottom to work even using reflection under McAfee.
                //
                private int MarginBottom => MarginTop;

                //
                // Cache the properties, but bewared that if the margin 
                // is changed at runtime, these cached values will not 
                // be updated. Adding an event handler for margin changed
                // will do it well, but it's unnecessary for us here.
                //
                private readonly int splitWidth = 0;
                private readonly int marginTop = 0;
                private readonly int marginBottom = 0;

                public ContextMenuStrip SplitMenu { get; set; }

                protected override void OnClick(EventArgs e)
                {
                    Point clickPoint = PointToClient(new Point(MousePosition.X, MousePosition.Y));

                    if (clickPoint.X < (Width - splitWidth))
                    {
                        base.OnClick(e);
                    }
                    else
                    {
                        ShowButtonMenu();
                    }
                }

                protected override void OnPaint(PaintEventArgs pevent)
                {
                    base.OnPaint(pevent);

                    //
                    // If you see any 'Weird' codes, that is because CRAZY McAfee.
                    //
                    pevent.Graphics.DrawLine(
                        new Pen(Color.Black, 1),
                        Width - splitWidth,
                        marginTop,
                        Width - splitWidth,
                        Height - marginBottom);
                }
                
                private void ShowButtonMenu()
                {
                    SplitMenu?.Show(
                        this, new Point(0, this.Height),
                        ToolStripDropDownDirection.BelowRight);
                }
            }
        }
    }
}
