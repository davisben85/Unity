# Unity utility scripts

To use:

* Add the BatchmodeBuilder.cs to an Editor directory in your project. 
* Generate a template via the BeMyTest > Generate > Template menu.
* The template.json will be added to your Assets dir.
* Copy/paste this file for each platform you wish to build for, and change the name to match the following pattern: <platform>_build.json (ios_build.json)
  
 Example: 
  
{
    "productName": "Game",
    "companyName": "MyCompany",
    "bundleId": "com.me.build",
    "bundleVersion": "1.0.1",
    "buildNumber": "0",
    "buildTarget": "ios",
    "customMethod": "",
    "buildOutput": "build/",
    "logOutputDir": "logs"
}
  
  The above config will build the xcode project, putting the output in Application.dataPath + /build/ios
  The `build.log` file will be placed in the Application.dataPath + /build/logs directory.
  
  
  Note: customMethod is _not_ used from this config
  
