using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

public class PageSwitcher
{
    private const double EdgeBuffer = 70.0;
    private const int AnimationDurationMs = 350;

    public Window WorkingWin { get; set; }
    public Grid Views { get; set; }
    private int _CurrentPage = -1;

    public bool Playing { get; set; } = false;

    public PageSwitcher(Window WorkingWin, Grid Views)
    {
        this.WorkingWin = WorkingWin;
        this.Views = Views;
    }

    private double GetViewH() => WorkingWin.ActualHeight + EdgeBuffer;
    private double GetViewW() => WorkingWin.ActualWidth + EdgeBuffer;

    private void FadeInPage(Grid Page)
    {
        Playing = true;
        Page.Opacity = 0;
        Page.Visibility = Visibility.Visible;
        var Anim = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(2));
        Anim.Completed += (s, e) => Playing = false;
        Page.BeginAnimation(UIElement.OpacityProperty, Anim);
    }

    public void SwitchPageByVertical(int Index)
    {
        var Pages = Views.Children.OfType<Grid>().ToList();
        if (Index < 0 || Index >= Pages.Count) return;
        if (_CurrentPage == Index) return;

        if (Playing)
        {
            PrepareTransform(Pages);
        }

        if (_CurrentPage == -1)
        {
            _CurrentPage = Index;
            FadeInPage(Pages[Index]);
            return;
        }

        Playing = true;

        int OldPageIndex = _CurrentPage;
        _CurrentPage = Index; 

        int TargetIndex = Index;

        for (int I = 0; I < Pages.Count; I++)
        {
            if (I != Index && I != OldPageIndex)
            {
                Pages[I].Visibility = Visibility.Collapsed;
            }
        }

        var OldPage = Pages[OldPageIndex];
        var NewPage = Pages[Index];

        OldPage.Visibility = Visibility.Visible;
        NewPage.Visibility = Visibility.Visible;

        var TOld = GetOrCreateTransform(OldPage);
        var TNew = GetOrCreateTransform(NewPage);

        TOld.BeginAnimation(TranslateTransform.XProperty, null);
        TOld.X = 0;
        TNew.BeginAnimation(TranslateTransform.XProperty, null);
        TNew.X = 0;

        double ViewH = GetViewH();
        bool MoveUp = Index > OldPageIndex;
        double OldTargetY = MoveUp ? -ViewH : ViewH;
        double NewStartY = MoveUp ? ViewH : -ViewH;

        var Ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        int CompletedCount = 0;
        Action CheckUnlock = () =>
        {
            CompletedCount++;
            if (CompletedCount >= 2)
            {
                if (_CurrentPage != TargetIndex)
                {
                    Playing = false;
                    int LatestTarget = _CurrentPage;
                    _CurrentPage = TargetIndex;
                    SwitchPageByVertical(LatestTarget);
                }
                else
                {
                    Playing = false; 
                }
            }
        };

        var OldAnim = new DoubleAnimation(OldTargetY, TimeSpan.FromMilliseconds(AnimationDurationMs)) { EasingFunction = Ease };
        OldAnim.Completed += (s, e) =>
        {
            OldPage.Visibility = Visibility.Collapsed;
            TOld.BeginAnimation(TranslateTransform.YProperty, null);
            TOld.Y = OldTargetY;
            CheckUnlock();
        };
        TOld.BeginAnimation(TranslateTransform.YProperty, OldAnim);

        var NewAnim = new DoubleAnimation(NewStartY, 0, TimeSpan.FromMilliseconds(AnimationDurationMs)) { EasingFunction = Ease };
        NewAnim.Completed += (s, e) =>
        {
            TNew.BeginAnimation(TranslateTransform.YProperty, null);
            TNew.Y = 0;
            CheckUnlock();
        };
        TNew.BeginAnimation(TranslateTransform.YProperty, NewAnim);
    }

    public void SwitchPageByHorizontal(int Index)
    {
        var Pages = Views.Children.OfType<Grid>().ToList();
        if (Index < 0 || Index >= Pages.Count) return;
        if (_CurrentPage == Index) return;

        if (Playing)
        {
            PrepareTransform(Pages);
        }

        if (_CurrentPage == -1)
        {
            _CurrentPage = Index;
            FadeInPage(Pages[Index]);
            return;
        }

        Playing = true;

        int OldPageIndex = _CurrentPage;
        _CurrentPage = Index;

        int TargetIndex = Index;

        for (int I = 0; I < Pages.Count; I++)
        {
            if (I != Index && I != OldPageIndex)
            {
                Pages[I].Visibility = Visibility.Collapsed;
            }
        }

        var OldPage = Pages[OldPageIndex];
        var NewPage = Pages[Index];

        OldPage.Visibility = Visibility.Visible;
        NewPage.Visibility = Visibility.Visible;

        var TOld = GetOrCreateTransform(OldPage);
        var TNew = GetOrCreateTransform(NewPage);

        TOld.BeginAnimation(TranslateTransform.YProperty, null);
        TOld.Y = 0;
        TNew.BeginAnimation(TranslateTransform.YProperty, null);
        TNew.Y = 0;

        double ViewW = GetViewW();
        bool MoveLeft = Index > OldPageIndex;
        double OldTargetX = MoveLeft ? -ViewW : ViewW;
        double NewStartX = MoveLeft ? ViewW : - ViewW;

        var Ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        int CompletedCount = 0;
        Action CheckUnlock = () =>
        {
            CompletedCount++;
            if (CompletedCount >= 2)
            {
                if (_CurrentPage != TargetIndex)
                {
                    Playing = false;
                    int LatestTarget = _CurrentPage;
                    _CurrentPage = TargetIndex;
                    SwitchPageByHorizontal(LatestTarget);
                }
                else
                {
                    Playing = false;
                }
            }
        };

        var OldAnim = new DoubleAnimation(OldTargetX, TimeSpan.FromMilliseconds(AnimationDurationMs)) { EasingFunction = Ease };
        OldAnim.Completed += (s, e) =>
        {
            OldPage.Visibility = Visibility.Collapsed;
            TOld.BeginAnimation(TranslateTransform.XProperty, null);
            TOld.X = OldTargetX;
            CheckUnlock();
        };
        TOld.BeginAnimation(TranslateTransform.XProperty, OldAnim);

        var NewAnim = new DoubleAnimation(NewStartX, 0, TimeSpan.FromMilliseconds(AnimationDurationMs)) { EasingFunction = Ease };
        NewAnim.Completed += (s, e) =>
        {
            TNew.BeginAnimation(TranslateTransform.XProperty, null);
            TNew.X = 0;
            CheckUnlock();
        };
        TNew.BeginAnimation(TranslateTransform.XProperty, NewAnim);
    }

    private void PrepareTransform(List<Grid> Pages)
    {
        foreach (var Page in Pages)
        {
            if (Page.RenderTransform is TranslateTransform T)
            {
                T.BeginAnimation(TranslateTransform.YProperty, null);
                T.BeginAnimation(TranslateTransform.XProperty, null);
            }
        }
    }

    private TranslateTransform GetOrCreateTransform(Grid Page)
    {
        if (!(Page.RenderTransform is TranslateTransform))
            Page.RenderTransform = new TranslateTransform();
        return (TranslateTransform)Page.RenderTransform;
    }
}