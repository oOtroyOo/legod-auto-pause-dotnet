using Android.App;

[assembly: UsesPermission("android.permission.INTERNET")]

// 前台服务权限（Android 8.0+ 必需）
[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE")]

// 通知权限（Android 13+ 必需）
[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]

// 通知权限（Android 13+ 必需）
[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE_REMOTE_MESSAGING")]
