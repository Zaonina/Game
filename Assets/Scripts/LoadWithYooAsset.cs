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
    /// ��Դϵͳ����ģʽ
    /// </summary>
    public PlayMode playMode;
    EPlayMode _playMode;
    private string _packageVersion = null;
    public string buildPipeline;
    public string[] currentGamePackages;

    //HybridCLR����
    private static Dictionary<string, byte[]> s_assetDatas = new Dictionary<string, byte[]>();
    

    void Start()
    {
        if (playMode == PlayMode.Editor) _playMode = EPlayMode.EditorSimulateMode;
        if (playMode == PlayMode.Host) _playMode = EPlayMode.HostPlayMode;
        
        StartCoroutine(Init());
        
    }

    private IEnumerator Init()
    {
        Debug.Log($"��Դϵͳ����ģʽ��{_playMode}");
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        YooAssets.Initialize();
        YooAssets.SetOperationSystemMaxTimeSlice(30);
        yield return StartCoroutine(InitPackage(buildPipeline));
    }

    #region YooAsset������Դ
    private IEnumerator InitPackage(string buildPipeline)
    {

        yield return UpdatePackage("Codes",buildPipeline);

        //����HybridClR����
        yield return StartCoroutine(StartLoadDll("Codes"));

        StartCoroutine(Run_InstantiateComponentByAsset());
    }

    private IEnumerator UpdatePackage(string packageName,string buildPipeline)
    {
        // ����Ĭ�ϵ���Դ��
        //packageName = "DefaultPackage";
        var package = YooAssets.TryGetPackage(packageName);
        if (package == null)
        {
            package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);
        }

        // �༭���µ�ģ��ģʽ
        InitializationOperation initializationOperation = null;
        if (_playMode == EPlayMode.EditorSimulateMode)
        {
            var createParameters = new EditorSimulateModeParameters();
            createParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(buildPipeline, packageName);
            initializationOperation = package.InitializeAsync(createParameters);

        }

        // ��������ģʽ
        // if (playMode == EPlayMode.OfflinePlayMode)
        // {
        //     var createParameters = new OfflinePlayModeParameters();
        //     createParameters.DecryptionServices = new FileStreamDecryption();
        //     initializationOperation = package.InitializeAsync(createParameters);
        //     text.text += "����ģʽ";
        // }

        // ��������ģʽ
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
            Debug.Log("��ʼ���ɹ�!");
            yield return StartCoroutine(GetStaticVersion(packageName));
        }
        else
        {
            Debug.LogWarning($"{initializationOperation.Error}");
        }
    }

    /// <summary>
    /// ��ȡ��Դ�汾
    /// </summary>
    /// <returns></returns>
    private IEnumerator GetStaticVersion(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageVersionAsync();
        yield return operation;

        if (operation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("��Դ�汾��ȡ�ɹ�");
            Debug.Log("�汾��:" + operation.PackageVersion);
            _packageVersion = operation.PackageVersion;
            yield return StartCoroutine(UpdateManifest(packageName));
        }
        else
        {
            Debug.LogWarning(operation.Error);
        }
    }

    /// <summary>
    /// ������Դ�嵥
    /// </summary>
    /// <returns></returns>
    private IEnumerator UpdateManifest(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageManifestAsync(_packageVersion);
        yield return operation;

        if (operation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("��Դ�嵥���³ɹ�");
            yield return StartCoroutine(CreateDownloader());
        }
        else
        {
            Debug.LogWarning(operation.Error);
        }
    }

    //����
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
            Debug.Log($"Found total {downloader.TotalDownloadCount} files that need download ��");

            // �����¸����ļ��󣬹�������ϵͳ
            // ע�⣺��������Ҫ������ǰ�����̿ռ䲻��
            int totalDownloadCount = downloader.TotalDownloadCount;
            long totalDownloadBytes = downloader.TotalDownloadBytes;
            //��ʼ����


            //ע��ص�����
            downloader.OnDownloadErrorCallback = OnDownloadErrorFunction;
            downloader.OnDownloadProgressCallback = OnDownloadProgressUpdateFunction;
            downloader.OnDownloadOverCallback = OnDownloadOverFunction;
            downloader.OnStartDownloadFileCallback = OnStartDownloadFileFunction;

            //��������
            downloader.BeginDownload();
            yield return downloader;

            //������ؽ��
            if (downloader.Status == EOperationStatus.Succeed)
            {
                //���سɹ�
                Debug.Log("���سɹ�!");
            }
            else
            {
                //����ʧ��

                //todo
                yield break;
            }
        }
        //StartCoroutine(DownOver());
        Debug.Log("ʹ��YooAsset�ȸ�����Դ���!");
    }


    private void OnStartDownloadFileFunction(string fileName, long sizeBytes)
    {
        Debug.Log("��ʼ����{0},��С:{1}");

    }

    private void OnDownloadOverFunction(bool isSucceed)
    {
        Debug.Log(isSucceed ? "�������" : "����ʧ��");
    }

    private void OnDownloadProgressUpdateFunction(int totalDownloadCount, int currentDownloadCount, long totalDownloadBytes, long currentDownloadBytes)
    {
        Debug.Log("��������:{0},�Ѿ�����{1},���ش�С{2},�Ѿ�����" + currentDownloadBytes / 1000000);
    }

    private void OnDownloadErrorFunction(string fileName, string error)
    {
        Debug.Log("���� {0} ʧ�� : {1}");
    }
    #endregion

    #region HybridCLRʹ��YooAsset�����ȸ��´���

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
            Debug.Log("Edģʽ��ִ�в���dll");
        }
        else
        {
            var assets = new List<string>
            {
                "HotUpdate.dll.bytes",
            }.Concat(AOTMetaAssemblyFiles);

            foreach (var assembly in assets)
            {
                //ʹ��YooAsset����ԭ���ļ�
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
        /// ע�⣬����Ԫ�����Ǹ�AOT dll����Ԫ���ݣ������Ǹ��ȸ���dll����Ԫ���ݡ�
        /// �ȸ���dll��ȱԪ���ݣ�����Ҫ���䣬�������LoadMetadataForAOTAssembly�᷵�ش���

        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in AOTMetaAssemblyFiles)
        {
            byte[] dllBytes = ReadBytesFromStreamingAssets(aotDllName);
            // ����assembly��Ӧ��dll�����Զ�Ϊ��hook��һ��aot���ͺ�����native���������ڣ��ý������汾����
            LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
            Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. mode:{mode} ret:{err}");
        }
    }


    #endregion

    //����Game
    IEnumerator Run_InstantiateComponentByAsset()
    {
        /*
           LoadSceneAsync() �첽���س���
           LoadAssetSync() ͬ��������Դ����
           LoadAssetAsync() �첽������Դ����
           LoadSubAssetsSync() ͬ����������Դ����
           LoadSubAssetsAsync() �첽��������Դ����
           LoadAllAssetsSync() ͬ��������Դ����������Դ����
           LoadAllAssetsAsync() �첽������Դ����������Դ����
           LoadRawFileSync() ͬ����ȡԭ���ļ�
           LoadRawFileAsync() �첽��ȡԭ���ļ�
        */
        
        foreach (string packageName in currentGamePackages)
        {
            yield return UpdatePackage(packageName, "BuiltinBuildPipeline");
        }

        //package.ClearUnusedCacheFilesAsync();
        //��ʼ��Ϸ:
        var package = YooAssets.GetPackage("DefaultPackage");
        var playerAss = package.LoadSceneAsync("Assets/Scenes/Game");
        yield return playerAss;

    }




    /// <summary>
    /// ��ȡ��Դ��������ַ
    /// </summary>
    private string GetHostServerURL()
    {
        //string hostServerIP = "http://10.0.2.2"; //��׿ģ������ַ
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
    /// Զ����Դ��ַ��ѯ������
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
    /// ��Դ�ļ������ؽ�����
    /// </summary>
    private class FileStreamDecryption : IDecryptionServices
    {
        /// <summary>
        /// ͬ����ʽ��ȡ���ܵ���Դ������
        /// ע�⣺��������������Դ�������ͷŵ�ʱ����Զ��ͷ�
        /// </summary>
        AssetBundle IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            BundleStream bundleStream = new BundleStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            managedStream = bundleStream;
            return AssetBundle.LoadFromStream(bundleStream, fileInfo.ConentCRC, GetManagedReadBufferSize());
        }

        /// <summary>
        /// �첽��ʽ��ȡ���ܵ���Դ������
        /// ע�⣺��������������Դ�������ͷŵ�ʱ����Զ��ͷ�
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
    /// ��Դ�ļ�ƫ�Ƽ��ؽ�����
    /// </summary>
    private class FileOffsetDecryption : IDecryptionServices
    {
        /// <summary>
        /// ͬ����ʽ��ȡ���ܵ���Դ������
        /// ע�⣺��������������Դ�������ͷŵ�ʱ����Զ��ͷ�
        /// </summary>
        AssetBundle IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            managedStream = null;
            return AssetBundle.LoadFromFile(fileInfo.FileLoadPath, fileInfo.ConentCRC, GetFileOffset());
        }

        /// <summary>
        /// �첽��ʽ��ȡ���ܵ���Դ������
        /// ע�⣺��������������Դ�������ͷŵ�ʱ����Զ��ͷ�
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
    /// ��Դ�ļ�������
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


#region YooAsset����������


/// <summary>
/// ��Դ�ļ���ѯ������
/// </summary>
public class GameQueryServices : IBuildinQueryServices
{
    /// <summary>
    /// ��ѯ�����ļ���ʱ���Ƿ�ȶ��ļ���ϣֵ
    /// </summary>
    public static bool CompareFileCRC = false;

    public bool Query(string packageName, string fileName, string fileCRC)
    {
        // ע�⣺fileName�����ļ���ʽ
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
    /// ��ʼ��
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
    /// �����ļ���ѯ����
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
    /// ��Ŀ¼���ƣ����ֺ�YooAssets��Դϵͳһ�£�
    /// </summary>
    public const string RootFolderName = "yoo";
}


/// <summary>
/// ������Դ�嵥
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