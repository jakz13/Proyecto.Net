#if ANDROID
using Android.Content;
using Android.Views;
using Android.Widget;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace UltimaMilla.Mobile.Platforms.Android;

public class CustomShellRenderer : ShellRenderer
{
    public CustomShellRenderer(Context context) : base(context)
    {
    }

    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem)
    {
        return new CustomShellItemRenderer(this);
    }
}

public class CustomShellItemRenderer : ShellItemRenderer
{
    public CustomShellItemRenderer(IShellContext shellContext) : base(shellContext)
    {
    }

    public override global::Android.Views.View OnCreateView(LayoutInflater inflater, ViewGroup container, global::Android.OS.Bundle savedInstanceState)
    {
        var view = base.OnCreateView(inflater, container, savedInstanceState);

        if (view is ViewGroup viewGroup)
        {
            CenterBottomNavLabels(viewGroup);
        }

        return view;
    }

    private void CenterBottomNavLabels(ViewGroup root)
    {
        for (int i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChildAt(i);
            if (child is BottomNavigationView bottomNav)
            {
                AdjustBottomNavigationView(bottomNav);
                return;
            }
            else if (child is ViewGroup subGroup)
            {
                CenterBottomNavLabels(subGroup);
            }
        }
    }

    private void AdjustBottomNavigationView(BottomNavigationView bottomNav)
    {
        bottomNav.Post(() =>
        {
            try
            {
                // El primer hijo de BottomNavigationView es BottomNavigationMenuView
                if (bottomNav.GetChildAt(0) is ViewGroup menuView)
                {
                    for (int i = 0; i < menuView.ChildCount; i++)
                    {
                        if (menuView.GetChildAt(i) is ViewGroup itemView)
                        {
                            // Ocultar icono vacío para centrar el label
                            for (int j = 0; j < itemView.ChildCount; j++)
                            {
                                var itemChild = itemView.GetChildAt(j);
                                if (itemChild is ImageView iconView)
                                {
                                    iconView.Visibility = ViewStates.Gone;
                                }
                                else if (itemChild is ViewGroup labelGroup)
                                {
                                    if (labelGroup.LayoutParameters is FrameLayout.LayoutParams lp)
                                    {
                                        lp.Gravity = GravityFlags.Center;
                                        labelGroup.LayoutParameters = lp;
                                    }

                                    for (int k = 0; k < labelGroup.ChildCount; k++)
                                    {
                                        if (labelGroup.GetChildAt(k) is TextView tv)
                                        {
                                            tv.Gravity = GravityFlags.Center;
                                            tv.TextAlignment = global::Android.Views.TextAlignment.Center;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // En caso de discrepancia de versión de Material Components, degradar de forma segura
            }
        });
    }
}
#endif
