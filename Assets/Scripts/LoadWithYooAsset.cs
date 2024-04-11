using System.Collections;
using UnityEngine;
using YooAsset;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HybridCLR;
using System.Reflection;
using System;

public enum PlayMode
{
    Editor,
    Host
    
}
public class LoadWithYooAsset : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public PlayMode playMode;
    EPlayMode _playMode;
    private string _packageVersion = null;
    public string buildPipeline;
    public string[] currentGamePackages;

    //HybridCLR部分
    private static Dictionary<string, byte[]> s_assetDatas = new Dictionary<string, byte[]>();
    

    void Start()
    {
        if (playMode == PlayMode.Editor) _playMode = EPlayMode.EditorSimulateMode;
        if (playMode == PlayMode.Host) _playMode = EPlayMode.HostPlayMode;
        
        StartCoroutine(Init());
        
    }

    private IEnumerator Init()
    {
        Debug.Log($"资源系统运行模式：{_playMode}");
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        YooAssets.Initialize();
        YooAssets.SetOperationSystemMaxTimeSlice(30);
        yield return StartCoroutine(InitPackage(buildPipeline));
    }

    #region YooAsset更新资源
    private IEnumerator InitPackage(string buildPipeline)
    {

        yield return UpdatePackage("Codes",buildPipeline);

        //进入HybridClR部分
        yield return StartCoroutine(StartLoadDll("Codes"));

        StartCoroutine(Run_InstantiateComponentByAsset());
    }

    private IEnumerator UpdatePackage(string packageName,string buildPipeline)
    {
        // 创建默认的资源包
        //packageName = "DefaultPackage";
        var package = YooAssets.TryGetPackage(packageName);
        if (package == null)
        {
            package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);
        }

        // 编辑器下的模拟模式
        InitializationOperation initializationOperation = null;
        if (_playMode == EPlayMode.EditorSimulateMode)
        {
            var createParameters = new EditorSimulateModeParameters();
            createParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(buildPipeline, packageName);
            initializationOperation = package.InitializeAsync(createParameters);

        }

        // 单机运行模式
        // if (playMode == EPlayMode.OfflinePlayMode)
        // {
        //     var createParameters = new OfflinePlayModeParameters();
        //     createParameters.DecryptionServices = new FileStreamDecryption();
        //     initializationOperation = package.InitializeAsync(createParameters);
        //     text.text += "单机模式";
        // }

        // 联机运行模式
        if (_playMode == EPlayMode.HostPlayMode)
        {
            string defaultHostServer = GetHostServerURL();
            string fallbackHostServer = GetHostServerURL();
            var createParameters = new HostPlayModeParameters();
            createParameters.DecryptionServices = new FileStreamDecryption();
            createParameters.BuildinQueryServices = new GameQueryServices();
            createParameters.RemoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
            initializationOperation = package.InitializeAsync(createParameters);

        }

        yield return initializationOperation;
        if (initializationOperation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("初始化成功!");
            yield return StartCoroutine(GetStaticVersion(packageName));
        }
        else
        {
            Debug.LogWarning($"{initializationOperation.Error}");
        }
    }

    /// <summary>
    /// 获取资源版本
    /// </summary>
    /// <returns></returns>
    private IEnumerator GetStaticVersion(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageVersionAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("资源版本获取成功");
            Debug.Log("版本号:" + operation.PackageVersion);
            _packageVersion = operation.PackageVersion;
            yield return StartCoroutine(UpdateManifest(packageName));
        }
        else
        {
            Debug.LogWarning(operation.Error);
        }
    }

    /// <summary>
    /// 更新资源清单
    /// </summary>
    /// <returns></returns>
    private IEnumerator UpdateManifest(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageManifestAsync(_packageVersion);
        yield return operation;

        if (operation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("资源清单更新成功");
            yield return StartCoroutine(CreateDownloader());
        }
        else
        {
            Debug.LogWarning(operation.Error);
        }
    }

    //下载
    IEnumerator CreateDownloader()
    {
        int downloadingMaxNum = 9999;
        int failedTryAgain = 3;
        var downloader = YooAssets.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("Not found any download files !");
        }
        else
        {
            //A total of 10 files were found that need to be downloaded
            Debug.Log($"Found total {downloader.TotalDownloadCount} files that need download ！");

            // 发现新更新文件后，挂起流程系统
            // 注意：开发者需要在下载前检测磁盘空间不足
            int totalDownloadCount = downloader.TotalDownloadCount;
            long totalDownloadBytes = downloader.TotalDownloadBytes;
            //开始下载


            //注册回调方法
            downloader.OnDownloadErrorCallback = OnDownloadErrorFunction;
            downloader.OnDownloadProgressCallback = OnDownloadProgressUpdateFunction;
            downloader.OnDownloadOverCallback = OnDownloadOverFunction;
            downloader.OnStartDownloadFileCallback = OnStartDownloadFileFunction;

            //开启下载
            downloader.BeginDownload();
            yield return downloader;

            //检测下载结果
            if (downloader.Status == EOperationStatus.Succeed)
            {
                //下载成功
                Debug.Log("下载成功!");
            }
            else
            {
                //下载失败

                //todo
                yield break;
            }
        }
        //StartCoroutine(DownOver());
        Debug.Log("使用YooAsset热更新资源完成!");
    }


    private void OnStartDownloadFileFunction(string fileName, long sizeBytes)
    {
        Debug.Log("开始下载{0},大小:{1}");

    }

    private void OnDownloadOverFunction(bool isSucceed)
    {
        Debug.Log(isSucceed ? "下载完成" : "下载失败");
    }

    private void OnDownloadProgressUpdateFunction(int totalDownloadCount, int currentDownloadCount, long totalDownloadBytes, long currentDownloadBytes)
    {
        Debug.Log("下载数量:{0},已经下载{1},下载大小{2},已经下载" + currentDownloadBytes / 1000000);
    }

    private void OnDownloadErrorFunction(string fileName, string error)
    {
        Debug.Log("下载 {0} 失败 : {1}");
    }
    #endregion

    #region HybridCLR使用YooAsset加载热更新代码

    private static List<string> AOTMetaAssemblyFiles { get; } = new List<string>()
        {
            "mscorlib.dll.bytes",
            "System.dll.bytes",
            "System.Core.dll.bytes",
        };
    public static byte[] ReadBytesFromStreamingAssets(string dllName)
    {
        return s_assetDatas[dllName];
    }

    private static Assembly _hotUpdateAss;
    IEnumerator StartLoadDll(string packageName)
    {
        yield return LoadAsset(packageName);
    }

    IEnumerator LoadAsset(string packageName)
    {
        if (_playMode != EPlayMode.HostPlayMode)
        {
            Debug.Log("Ed模式不执行补偿dll");
        }
        else
        {
            var assets = new List<string>
            {
                "HotUpdate.dll.bytes",
            }.Concat(AOTMetaAssemblyFiles);

            foreach (var assembly in assets)
            {
                //使用YooAsset加载原生文件
                var package = YooAssets.GetPackage(packageName);
                string str = assembly.Replace(".bytes","");
                str = "Assets/Codes/" + str;
                var handle = package.LoadRawFileAsync(str);
                yield return handle;
                byte[] fileDate = handle.GetRawFileData();
                s_assetDatas[assembly] = fileDate;
                Debug.Log($"dll:" + assembly + "," + "size:" + fileDate.Length);

            }
            LoadMetadataForAOTAssemblies();
        }
        
        
#if !UNITY_EDITOR
            _hotUpdateAss = Assembly.Load(ReadBytesFromStreamingAssets("HotUpdate.dll.bytes"));
#else
        _hotUpdateAss = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif
        //Type entryType = _hotUpdateAss.GetType("Entry");
        //entryType.GetMethod("Start").Invoke(null, null);
        
        
    }

    private void LoadMetadataForAOTAssemblies()
    {
        /// 注意，补充元数据是给AOT dll补充元数据，而不是给热更新dll补充元数据。
        /// 热更新dll不缺元数据，不需要补充，如果调用LoadMetadataForAOTAssembly会返回错误

        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in AOTMetaAssemblyFiles)
        {
            byte[] dllBytes = ReadBytesFromStreamingAssets(aotDllName);
            // 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
            LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
            Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. mode:{mode} ret:{err}");
        }
    }


    #endregion

    //加载Game
    IEnumerator Run_InstantiateComponentByAsset()
    {
        /*
           LoadSceneAsync() 异步加载场景
           LoadAssetSync() 同步加载资源对象
           LoadAssetAsync() 异步加载资源对象
           LoadSubAssetsSync() 同步加载子资源对象
           LoadSubAssetsAsync() 异步加载子资源对象
           LoadAllAssetsSync() 同步加载资源包内所有资源对象
           LoadAllAssetsAsync() 异步加载资源包内所有资源对象
           LoadRawFileSync() 同步获取原生文件
           LoadRawFileAsync() 异步获取原生文件
        */
        
        foreach (string packageName in currentGamePackages)
        {
            yield return UpdatePackage(packageName, "BuiltinBuildPipeline");
        }

        //package.ClearUnusedCacheFilesAsync();
        //开始游戏:
        var package = YooAssets.GetPackage("DefaultPackage");
        var playerAss = package.LoadSceneAsync("Assets/Scenes/Game");
        yield return playerAss;

    }




    /// <summary>
    /// 获取资源服务器地址
    /// </summary>
    private string GetHostServerURL()
    {
        //string hostServerIP = "http://10.0.2.2"; //安卓模拟器地址
        string hostServerIP = "http://127.0.0.1";
        string appVersion = "v1.0";

#if UNITY_EDITOR
        if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            return $"{hostServerIP}/CDN/Android/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            return $"{hostServerIP}/CDN/IPhone/{appVersion}";
        else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
            return $"{hostServerIP}/CDN/WebGL/{appVersion}";
        else
            return $"{hostServerIP}/CDN/PC/{appVersion}";
#else
        if (Application.platform == RuntimePlatform.Android)
            return $"{hostServerIP}/CDN/Android/{appVersion}";
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
            return $"{hostServerIP}/CDN/IPhone/{appVersion}";
        else if (Application.platform == RuntimePlatform.WebGLPlayer)
            return $"{hostServerIP}/CDN/WebGL/{appVersion}";
        else
            return $"{hostServerIP}/CDN/PC/{appVersion}";
#endif
    }

    /// <summary>
    /// 远端资源地址查询服务类
    /// </summary>
    private class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }
        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }
        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }

    /// <summary>
    /// 资源文件流加载解密类
    /// </summary>
    private class FileStreamDecryption : IDecryptionServices
    {
        /// <summary>
        /// 同步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundle IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            BundleStream bundleStream = new BundleStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            managedStream = bundleStream;
            return AssetBundle.LoadFromStream(bundleStream, fileInfo.ConentCRC, GetManagedReadBufferSize());
        }

        /// <summary>
        /// 异步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundleCreateRequest IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            BundleStream bundleStream = new BundleStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            managedStream = bundleStream;
            return AssetBundle.LoadFromStreamAsync(bundleStream, fileInfo.ConentCRC, GetManagedReadBufferSize());
        }

        private static uint GetManagedReadBufferSize()
        {
            return 1024;
        }
    }

    /// <summary>
    /// 资源文件偏移加载解密类
    /// </summary>
    private class FileOffsetDecryption : IDecryptionServices
    {
        /// <summary>
        /// 同步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundle IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            managedStream = null;
            return AssetBundle.LoadFromFile(fileInfo.FileLoadPath, fileInfo.ConentCRC, GetFileOffset());
        }

        /// <summary>
        /// 异步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundleCreateRequest IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            managedStream = null;
            return AssetBundle.LoadFromFileAsync(fileInfo.FileLoadPath, fileInfo.ConentCRC, GetFileOffset());
        }

        private static ulong GetFileOffset()
        {
            return 32;
        }
    }

    /// <summary>
    /// 资源文件解密流
    /// </summary>
    public class BundleStream : FileStream
    {
        public const byte KEY = 64;

        public BundleStream(string path, FileMode mode, FileAccess access, FileShare share) : base(path, mode, access, share)
        {
        }
        public BundleStream(string path, FileMode mode) : base(path, mode)
        {
        }

        public override int Read(byte[] array, int offset, int count)
        {
            var index = base.Read(array, offset, count);
            for (int i = 0; i < array.Length; i++)
            {
                array[i] ^= KEY;
            }
            return index;
        }
    }
}


#region YooAsset更新依赖类


/// <summary>
/// 资源文件查询服务类
/// </summary>
public class GameQueryServices : IBuildinQueryServices
{
    /// <summary>
    /// 查询内置文件的时候，是否比对文件哈希值
    /// </summary>
    public static bool CompareFileCRC = false;

    public bool Query(string packageName, string fileName, string fileCRC)
    {
        // 注意：fileName包含文件格式
        return StreamingAssetsHelper.FileExists(packageName, fileName, fileCRC);
    }
}

#if UNITY_EDITOR
public sealed class StreamingAssetsHelper
{
    public static void Init() { }
    public static bool FileExists(string packageName, string fileName, string fileCRC)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, StreamingAssetsDefine.RootFolderName, packageName, fileName);
        //string filePath = Path.Combine(Application.dataPath, StreamingAssetsDefine.RootFolderName, packageName, fileName);
        Debug.LogWarning(filePath);
        if (File.Exists(filePath))
        {
            if (GameQueryServices.CompareFileCRC)
            {
                string crc32 = YooAsset.Editor.EditorTools.GetFileCRC32(filePath);
                return crc32 == fileCRC;
            }
            else
            {
                return true;
            }
        }
        else
        {
            return false;
        }
    }
}
#else
public sealed class StreamingAssetsHelper
{
    private class PackageQuery
    {
        public readonly Dictionary<string, BuildinFileManifest.Element> Elements = new Dictionary<string, BuildinFileManifest.Element>(1000);
    }

    private static bool _isInit = false;
    private static readonly Dictionary<string, PackageQuery> _packages = new Dictionary<string, PackageQuery>(10);

    /// <summary>
    /// 初始化
    /// </summary>
    public static void Init()
    {
        if (_isInit == false)
        {
            _isInit = true;

            var manifest = Resources.Load<BuildinFileManifest>("BuildinFileManifest");
            if (manifest != null)
            {
                foreach (var element in manifest.BuildinFiles)
                {
                    if (_packages.TryGetValue(element.PackageName, out PackageQuery package) == false)
                    {
                        package = new PackageQuery();
                        _packages.Add(element.PackageName, package);
                    }
                    package.Elements.Add(element.FileName, element);
                }
            }
        }
    }

    /// <summary>
    /// 内置文件查询方法
    /// </summary>
    public static bool FileExists(string packageName, string fileName, string fileCRC32)
    {
        if (_isInit == false)
            Init();

        if (_packages.TryGetValue(packageName, out PackageQuery package) == false)
            return false;

        if (package.Elements.TryGetValue(fileName, out var element) == false)
            return false;

        if (GameQueryServices.CompareFileCRC)
        {
            return element.FileCRC32 == fileCRC32;
        }
        else
        {
            return true;
        }
    }
}
#endif

public class StreamingAssetsDefine
{
    /// <summary>
    /// 根目录名称（保持和YooAssets资源系统一致）
    /// </summary>
    public const string RootFolderName = "yoo";
}


/// <summary>
/// 内置资源清单
/// </summary>
public class BuildinFileManifest : ScriptableObject
{
    [Serializable]
    public class Element
    {
        public string PackageName;
        public string FileName;
        public string FileCRC32;
    }

    public List<Element> BuildinFiles = new List<Element>();
}

#endregion