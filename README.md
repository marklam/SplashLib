# Splash Screen for .net apps on Windows

This is based on the SplashLib library from ["Creating a Native Win32 Splash Screen"](https://weblogs.asp.net/bsimser/creating-a-native-win32-splash-screen) by [Bill Simser](https://github.com/bsimser), published Wednesday, June 11, 2008.

I retrieved the original source code archive from the [Internet Archive](https://web.archive.org/web/20160304001345/http://weblogs.asp.net/bsimser/creating-a-native-win32-splash-screen) and that's the initial commit in this repository.

I've updated the code for .net 8, and removed Windows Forms dependencies from the SplashLib class library (which means you now can't specify a form to activate when closing the splash screen).

There are also a few edits for null safety and modern C# syntax (at least as defined by Resharper).
