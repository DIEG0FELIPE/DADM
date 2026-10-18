namespace reto8
{
    public static class Toast
    {
        /// <summary>Muestra un mensaje breve (Toast nativo en Android).</summary>
        public static void Show(Page page, string message)
        {
#if ANDROID
            Android.Widget.Toast.MakeText(Platform.AppContext, message, Android.Widget.ToastLength.Short)?.Show();
#else
            _ = page.DisplayAlertAsync(null, message, "OK");
#endif
        }
    }
}
