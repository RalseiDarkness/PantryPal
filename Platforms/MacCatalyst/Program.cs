using ObjCRuntime;
using UIKit;

namespace PantryPal;

/// Native Mac Catalyst entry point that starts the MAUI AppDelegate.
public class Program
{
	// This is the main entry point of the application.
	static void Main(string[] args)
	{
		// if you want to use a different Application Delegate class from "AppDelegate"
		// you can specify it here.
		UIApplication.Main(args, null, typeof(AppDelegate));
	}
}
