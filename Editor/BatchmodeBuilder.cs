using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BatchmodeBuilder
{
   //menus
   private const string MENU_BASE_STRING = "BeMyTest/";
   private const string BUILD_MENU = MENU_BASE_STRING + "Build/";
   private const string IOS_BUILD_MENU = BUILD_MENU + "iOS";
   private const string ANDROID_BUILD_MENU = BUILD_MENU + "Android";
   private const string WEBGL_BUILD_MENU = BUILD_MENU + "WebGL";
   private const string STANDALONE_BUILD_MENU = BUILD_MENU + "Standalone";
   private const string UWP_BUILD_MENU = BUILD_MENU + "UWP";
   private const string TEMPLATE_MENU = MENU_BASE_STRING + "Generate/Template";
   private const string HELP_MENU = MENU_BASE_STRING + "Help";
   //config names
   private const string TEMPLATE_CONFIG_FILE_NAME = "template.json";
   private const string IOS_CONFIG_FILE_NAME = "ios_build.json";
   private const string ANDROID_CONFIG_FILE_NAME = "android_build.json";
   private const string UWP_CONFIG_FILE_NAME = "uwp_build.json";
   private const string WEBGL_CONFIG_FILE_NAME = "webgl_build.json";
   private const string STANDALONE_CONFIG_FILE_NAME = "standalone_build.json";
   //Unity const
   private const string OSX_EDITOR_LOG = "~/Library/Logs/Unity/Editor.log";
   private const string WIN_EDITOR_LOG = "%LOCALAPPDATA%\\Unity\\Editor\\Editor.log";
   private const string LINUX_EDITOR_LOG = "~/.config/unity3d/Editor.log";

   public static string logFile = "";

   [MenuItem(TEMPLATE_MENU)]
   //Generates an template json file, so it can be copied/filled in
   public static void GenerateTemplateConfigurationFile()
   {
      var configFile = GetConfigurationPathForPlatform();

      using (StreamWriter file = File.CreateText(configFile))
      {
         var buildConfigurationTemplate = new BuildConfiguration("productName", "companyName", "com.me.build", "bundleVersion", "0", "buildTarget", "customMethod", "buildOutput", "logOutputDir");
         var configuration = EditorJsonUtility.ToJson(buildConfigurationTemplate, true);
         file.Write(configuration);
      }

      EditorUtility.RevealInFinder(configFile);
   }


   [MenuItem(HELP_MENU)]
   //Logs some info about what the supported values are
   //TODO: Add some real validation vs relying on failures
   public static void HelpInfo()
   {
      Debug.LogWarning("Supported targets are: ios, android, windows, windows_64, osx, linux, webgl");
      Debug.LogWarning("Supported target groups are: ios, android, standalone, webgl");
   }
   //Generic method that loads the config chosen and runs the build
   public static void BuildPlatformFromConfig(string platformFileName)
   {
      //Load config
      var configFile = GetConfigurationPathForPlatform(platformFileName);
      if (!File.Exists(configFile))
      {
         throw new FileNotFoundException("Configuration file not found");
      };
      //parse config
      var buildConfig = GetBuildConfiguration(configFile);
      //set options
      var buildOptions = SetBuildPlayerOptions(buildConfig);
      //change build target
      EditorUserBuildSettings.SwitchActiveBuildTarget(buildOptions.targetGroup, buildOptions.target);

      try
      {
         //Build
         var results = BuildPipeline.BuildPlayer(buildOptions);

         //Maybe override the log output, and write to it
         if (buildConfig.logOutputDir != "")
         {
            OverrideLogOutput(buildConfig);
            WriteLogs();
         }

         if (results.summary.result != BuildResult.Succeeded)
         {
            throw new BuildFailedException("Build failed.");
         }

      }
      catch (BuildFailedException e)
      {
         Debug.LogError(e.StackTrace);
         Debug.LogError(e.Message);
         Debug.LogError(e.Data);
         HelpInfo();
      }
      finally
      {
         Debug.Log("The build has finished.");
      }

   }
   //projectRoot/<build_output>/<log_output>/build.log
   private static void OverrideLogOutput(BuildConfiguration buildConfig)
   {
      if (buildConfig.logOutputDir != "")
      {
         var root = Directory.GetParent(Application.dataPath);
         var logPath = Path.Combine(root.FullName.ToString(), buildConfig.buildOutput, buildConfig.logOutputDir);
         if (!Directory.Exists(logPath))
         {
            Directory.CreateDirectory(logPath);
         }
         logFile = Path.Combine(logPath, "build.log");
      }
   }

   //Reads Editor.log output when building, writes it to a custom location for consumption
   private static void WriteLogs()
   {
      var editorLogLocation = GetEditorLogs();

      using (FileStream fs = new FileStream(editorLogLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
         using (StreamReader sr = new StreamReader(fs))
         {
            while (sr.Peek() >= 0)
            {
               File.AppendAllText(logFile, sr.ReadLine() + "\n");
            }
         }
      }
   }
   //Gets platform specific Editor.log file, expanding env vars to get an absolute path
   private static string GetEditorLogs()
   {
      switch (Application.platform)
      {
         case RuntimePlatform.OSXEditor: return Expanded(OSX_EDITOR_LOG);
         case RuntimePlatform.WindowsEditor: return Expanded(WIN_EDITOR_LOG);
         case RuntimePlatform.LinuxEditor: return Expanded(LINUX_EDITOR_LOG);
         default: throw new Exception("Editor platform not detected");
      }
   }
   //Helper to expand env vars
   private static string Expanded(string pathWithEnvVars)
   {
      return Environment.ExpandEnvironmentVariables(pathWithEnvVars);
   }
   //Sets the BuildPlayerOptions from the json config file
   private static BuildPlayerOptions SetBuildPlayerOptions(BuildConfiguration config)
   {
      BuildPlayerOptions buildOptions = new BuildPlayerOptions();

      buildOptions.targetGroup = config.GetBuildTargetGroup();
      buildOptions.locationPathName = Path.Combine(config.buildOutput, config.buildTarget.ToLower());
      buildOptions.scenes = GetActiveScenes();
      buildOptions.target = config.GetBuildTarget();

      //Platform specific options
      if (config.GetBuildTarget() == BuildTarget.iOS)
      {
         PlayerSettings.iOS.buildNumber = config.buildNumber;
      }

      if (config.GetBuildTarget() == BuildTarget.Android)
      {
         PlayerSettings.Android.bundleVersionCode = Int32.Parse(config.buildNumber);
      }

      PlayerSettings.bundleVersion = config.bundleVersion;
      PlayerSettings.SetApplicationIdentifier(buildOptions.targetGroup, config.bundleId);

      return buildOptions;
   }
   //Helper to get scenes marked active in the project.  
   private static string[] GetActiveScenes()
   {
      return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
   }

   //Static methods

   [MenuItem(IOS_BUILD_MENU)]
   //invoked by -executeMethod BatchmodeBuilder.BuildiOS
   public static void BuildiOS()
   {
      //buildOutput is a dir
      BuildPlatformFromConfig(IOS_CONFIG_FILE_NAME);
   }

   [MenuItem(ANDROID_BUILD_MENU)]
   //invoked by -executeMethod BatchmodeBuilder.BuildAndroid
   public static void BuildAndroid()
   {
      //buildOutput should be an .apk
      //TODO: Support aab
      BuildPlatformFromConfig(ANDROID_CONFIG_FILE_NAME);
   }

   [MenuItem(WEBGL_BUILD_MENU)]
   //invoked by -executeMethod BatchmodeBuilder.BuildWebGL
   public static void BuildWebGL()
   {
      //buildOutput is a dir
      BuildPlatformFromConfig(WEBGL_CONFIG_FILE_NAME);
   }

   [MenuItem(STANDALONE_BUILD_MENU)]
   //invoked by -executeMethod BatchmodeBuilder.BuildStandalone
   public static void BuildStandalone()
   {
      //buildOutput is an .exe, .app, or dir depending on the platform
      BuildPlatformFromConfig(STANDALONE_CONFIG_FILE_NAME);
   }

   [MenuItem(UWP_BUILD_MENU)]
   //invoked by -executeMethod BatchmodeBuilder.BuildUWP
   public static void BuildUWP()
   {
      //buildOutput is a dir, sln file will be created
      BuildPlatformFromConfig(UWP_CONFIG_FILE_NAME);
   }

   //Get the config file location 
   private static string GetConfigurationPathForPlatform(string platformFileName = TEMPLATE_CONFIG_FILE_NAME)
   {
      return Path.Combine(Application.dataPath, platformFileName);
   }

   //Read config from config file
   public static BuildConfiguration GetBuildConfiguration(string configFilePath)
   {
      using (StreamReader file = new StreamReader(configFilePath))
      {
         var configContents = file.ReadToEnd();
         return JsonUtility.FromJson<BuildConfiguration>(configContents);
      }
   }
}

//Configuration class
[Serializable]
public class BuildConfiguration
{
   public string productName;
   public string companyName;
   public string bundleId;
   public string bundleVersion;
   public string buildNumber;
   public string customMethod;
   public string buildOutput;
   public string logOutputDir;
   public string buildTarget;

   public BuildConfiguration(string productName, string companyName, string bundleId, string bundleVersion, string buildNumber, string buildTarget, string customMethod, string buildOutput, string logOutputDir)
   {

      this.productName = productName;
      this.companyName = companyName;
      this.bundleId = bundleId;
      this.bundleVersion = bundleVersion;
      this.buildNumber = buildNumber;
      this.customMethod = customMethod;
      this.buildOutput = buildOutput;
      this.logOutputDir = logOutputDir;
      this.buildTarget = buildTarget;

   }

   public BuildTargetGroup GetBuildTargetGroup()
   {
      switch (buildTarget.ToLower())
      {
         case "ios": return BuildTargetGroup.iOS;
         case "android": return BuildTargetGroup.Android;
         case "standalone": return BuildTargetGroup.Standalone;
         case "webgl": return BuildTargetGroup.WebGL;
         default: return BuildTargetGroup.Standalone;
      }
   }

   public BuildTarget GetBuildTarget()
   {
      switch (buildTarget.ToLower())
      {
         case "ios": return BuildTarget.iOS;
         case "android": return BuildTarget.Android;
         case "windows": return BuildTarget.StandaloneWindows;
         case "windows_64": return BuildTarget.StandaloneWindows64;
         case "osx": return BuildTarget.StandaloneOSX;
         case "linux": return BuildTarget.StandaloneLinux64;
         case "webgl": return BuildTarget.WebGL;
         default: return BuildTarget.StandaloneWindows;
      }
   }
}
