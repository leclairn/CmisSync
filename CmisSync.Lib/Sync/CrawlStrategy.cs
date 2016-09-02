using DotCMIS.Client;
using DotCMIS.Exceptions;
using System;
using System.Linq;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using DotCMIS.Client.Impl;
using CmisSync.Lib.Cmis;
using DotCMIS.Enums;
using DotCMIS;

namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Part of CmisRepo.
    /// </summary>
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder
        {
            /// <summary>
            /// Synchronize by checking all folders/files one-by-one.
            /// This strategy is used if the CMIS server does not support the ChangeLog feature.
            /// 
            /// for all remote folders:
            ///     if exists locally:
            ///       recurse
            ///     else
            ///       if in database:
            ///         delete recursively from server // if BIDIRECTIONAL
            ///       else
            ///         download recursively
            /// for all remote files:
            ///     if exists locally:
            ///       if remote is more recent than local:
            ///         download
            ///       else
            ///         upload                         // if BIDIRECTIONAL
            ///     else:
            ///       if in database:
            ///         delete from server             // if BIDIRECTIONAL
            ///       else
            ///         download
            /// for all local files:
            ///   if not present remotely:
            ///     if in database:
            ///       delete
            ///     else:
            ///       upload                           // if BIDIRECTIONAL
            ///   else:
            ///     if has changed locally:
            ///       upload                           // if BIDIRECTIONAL
            /// for all local folders:
            ///   if not present remotely:
            ///     if in database:
            ///       delete recursively from local
            ///     else:
            ///       upload recursively               // if BIDIRECTIONAL
            /// </summary>
            /// <returns>
            /// True if all content has been successfully synchronized.
            /// False if anything has failed or been skipped.
            /// </returns>
            private bool CrawlSync(IFolder remoteFolder, string remotePath, string localFolder)
            {
                SleepWhileSuspended();

                /*if (IsGetDescendantsSupported)  Disabled because it causes server-side problems for folders with a huge number of files.
                {
                    IList<ITree<IFileableCmisObject>> desc;
                    try
                    {
                        desc = remoteFolder.GetDescendants(-1);
                    }
                    catch (DotCMIS.Exceptions.CmisConnectionException ex)
                    {
                        if (ex.InnerException is System.Xml.XmlException)
                        {
                            Logger.Warn(String.Format("CMIS::getDescendants() response could not be parsed: {0}", ex.InnerException.Message));
                        }
                        throw;
                    }
                    CrawlDescendants(remoteFolder, desc, localFolder);
                }*/

                // Lists of files/folders, to delete those that have been removed on the server.
                IList<string> remoteFiles = new List<string>();
                IList<string> remoteSubfolders = new List<string>();

                // Crawl remote children.
                Logger.InfoFormat("Crawl remote folder {0}", remotePath);
                bool success = CrawlRemote(remoteFolder, remotePath, localFolder, remoteFiles, remoteSubfolders);

                // Crawl local files.
                Logger.InfoFormat("Crawl local files in the local folder {0}", localFolder);
                CheckLocalFiles(localFolder, remoteFolder, remoteFiles);

                // Crawl local folders.
                Logger.InfoFormat("Crawl local folder {0}", localFolder);
                CheckLocalFolders(localFolder, remoteFolder, remoteSubfolders);

                return success;
            }


            private bool CrawlSyncQuery(IFolder remoteFolder, string remoteFolderPath, string localFolder)
            {
                bool success = true;
                try
                {
                    //IList<string> remoteFolders = new List<string>();

                    //IList<string> localFolders = database.getfoldersId();

                    IList<string> qFolders = queryAddedAndModifiedFolders(remoteFolder);


                    // Folders
                    foreach (string fId in qFolders)
                    {
                        try
                        {
                            IFolder folder = session.GetObject(fId) as IFolder;

                            SyncItem newFolderItem = SyncItemFactory.CreateFromRemoteFolder(folder.Path, repoInfo, database);

                            // Check if the folder already exists
                            IList<SyncItem> localfolders = database.GetAllFoldersWithCmisId(fId);
                            if (localfolders != null)
                            {
                                foreach (SyncItem oldFolderItem in localfolders)
                                {
                                    // if directory did not exists -> Move
                                    if (!Directory.Exists(newFolderItem.LocalPath))
                                    {
                                        //Directory.CreateDirectory(newFolderItem.LocalPath);
                                        //Directory.Delete(newFolderItem.LocalPath);
                                        Directory.Move(oldFolderItem.LocalPath, newFolderItem.LocalPath);
                                        database.MoveFolder(oldFolderItem, newFolderItem);
                                        var test = database.getMetadataInfo();
                                    }

                                    /*
                                    else
                                    {
                                        // Directory already exists, move content of the old folder and update database
                                        

                                        // Files
                                        foreach (string filePath in Directory.GetFiles(oldFolderItem.LocalPath))
                                        {
                                            string fileName = Path.GetFileName(filePath);
                                            string newFilePath = Path.Combine(newFolderItem.LocalPath, fileName);

                                            File.Move(filePath, newFilePath);
                                            SyncItem oldFileItem = database.GetSyncItemFromLocalPath(filePath);
                                            if (oldFileItem == null)
                                                oldFileItem = SyncItemFactory.CreateFromLocalPath(filePath, false, repoInfo, database);

                                            SyncItem newFileItem = SyncItemFactory.CreateFromLocalPath(newFilePath, false, repoInfo, database);

                                            if (Utils.IsMetadataFile(newFilePath))
                                            {
                                                database.MoveMetadataFile(oldFileItem, newFileItem, newFileItem.LocalPath + ".metadata");
                                            }
                                            else
                                            {
                                                database.MoveFile(oldFileItem, newFileItem);
                                            }
                                        }
                                        */
                                }
                            }
                            Directory.CreateDirectory(newFolderItem.LocalPath);
                        }
                        catch (CmisBaseException e)
                        {
                            ProcessRecoverableException("Could not access remote folder : ", e);
                            success = false;
                        }
                    }

                    IList<string> qDocs = queryAddedAndModifiedFiles(remoteFolder);

                    IList<string> remoteFiles = new List<string>();
                    // Files
                    foreach (string docId in qDocs)
                    {
                        try
                        {
                            IDocument doc = session.GetObject(docId) as IDocument;

                            SyncItem syncItem = SyncItemFactory.CreateFromRemoteDocument(doc.Paths[0], doc, repoInfo, database);

                            // Check if the file already exists
                            string id = docId.Remove(docId.IndexOf(';'));
                            string filePath = database.GetFilePath(id);
                            if (filePath != null)
                            {
                                // File already exists, delete
                                File.Delete(filePath);
                                SyncItem si = database.GetSyncItemFromLocalPath(filePath);
                                database.RemoveFile(si);

                                // Delete Metadata
                                if (database.ContainsLocalMetadata(filePath + ".metadata"))
                                {
                                    File.Delete(filePath + ".metadata");
                                    database.RemoveMetadataFile(si);
                                }
                            }
                            CrawlRemoteDocument(doc, syncItem.RemotePath, syncItem.LocalPath, remoteFiles);
                        }
                        catch (CmisBaseException e)
                        {
                            ProcessRecoverableException("Could not access remote document : ", e);
                            success = false;
                        }
                    }
                    database.setLastSyncDate(DateTimeOffset.Now.ToString("O"));
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not access remote objects: ", e);
                    success = false;
                }
                catch (Exception e)
                {
                    Logger.Error("Error in CrawlSyncQuery", e);
                    success = false;
                }
                return success;
            }

            private IList<string> queryAddedAndModifiedFiles(IFolder remoteFolder)
            {
                string PATH = Utils.getPATH(remoteFolder.Path);

                string query = "SELECT * FROM cmis:document where CONTAINS ('PATH:\""
                    + PATH + "\"')"
                    + " AND cmis:lastModificationDate > " + "'" + database.getLastSyncDate() + "'";

                var results = session.Query(query, false);

                List<string> filesId = new List<string>();

                foreach (IQueryResult r in results)
                {
                    filesId.Add((string)r.GetPropertyValueById(PropertyIds.ObjectId));
                }

                return filesId;
            }

            private IList<string> queryAddedAndModifiedFolders(IFolder remoteFolder)
            {
                string PATH = Utils.getPATH(remoteFolder.Path);

                string query = "SELECT * FROM cmis:folder where CONTAINS ('PATH:\""
                    + PATH + "\"')"
                    + " AND cmis:lastModificationDate > " + "'" + database.getLastSyncDate() + "'";

                var results = session.Query(query, false);

                List<string> foldersId = new List<string>();

                foreach (IQueryResult r in results)
                {
                    foldersId.Add((string)r.GetPropertyValueById(PropertyIds.ObjectId));
                }

                return foldersId;
            }


            /// <summary>
            /// Perform a crawl sync (check all folders and file checksums).
            /// If successful, update the local ChangeLog token.
            /// </summary>
            private void CrawlSyncAndUpdateChangeLogToken(IFolder remoteFolder, string remotePath, string localFolder)
            {
                var sw = new System.Diagnostics.Stopwatch();
                Logger.Info("Remote Full Crawl Started");
                try
                {
                    // Get ChangeLog token.
                    string token = CmisUtils.GetChangeLogToken(session);

                    // Sync.
                    bool success = CrawlSync(remoteFolder, remotePath, localFolder);

                    // Update ChangeLog token if sync has been successful.
                    if (success)
                    {
                        database.SetChangeLogToken(token);
                    }
                    else
                    {
                        Logger.Info("ChangeLog token not updated as an error occurred during sync.");
                    }
                }
                finally
                {
                    Logger.InfoFormat("Remote Full Crawl Finished : {0} min", sw.Elapsed);
                }
            }


            /// <summary>
            /// Crawl remote content, syncing down if needed.
            /// Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private bool CrawlRemote(IFolder remoteFolder, string remotePath, string localFolder, IList<string> remoteFiles, IList<string> remoteFolders)
            {
                bool success = true;
                SleepWhileSuspended();

                // Remember seen names, and depending on the profile ignore the ones that have appeared already.
                // For instance, if the CMIS server has a file called Hello and a folder called HELLO, then on Windows the paths would conflict.
                // Store lowercase file and folder names inside.
                HashSet<string> names = new HashSet<string>();

                // Get all remote children.
                // TODO: use paging
                IOperationContext operationContext = session.CreateOperationContext();
                this.repoInfo.CmisProfile.ConfigureOperationContext(operationContext);
                operationContext.MaxItemsPerPage = Int32.MaxValue;
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren(operationContext))
                {
                    try
                    {
                        if (cmisObject is DotCMIS.Client.Impl.Folder)
                        {
                            // It is a CMIS folder.
                            IFolder remoteSubFolder = (IFolder)cmisObject;
                            string remoteSubPath = CmisUtils.PathCombine(remotePath, remoteSubFolder.Name);
                            if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames && names.Contains(remoteSubFolder.Name.ToLowerInvariant()))
                            {
                                Logger.Warn("Ignoring " + remoteSubFolder.Name + "because other file or folder has the same name when ignoring lowercase/uppercase");
                            }
                            else
                            {
                                CrawlRemoteFolder(remoteSubFolder, remoteSubPath, localFolder, remoteFolders);
                                names.Add(remoteSubFolder.Name.ToLowerInvariant());
                            }
                        }
                        else if (cmisObject is DotCMIS.Client.Impl.Document)
                        {
                            // It is a CMIS document.
                            IDocument remoteDocument = (IDocument)cmisObject;
                            string remoteDocumentPath = CmisUtils.PathCombine(remotePath, remoteDocument.Name);
                            if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames && names.Contains(remoteDocument.Name.ToLowerInvariant()))
                            {
                                Logger.Warn("Ignoring " + remoteDocument.Name + "because other file or folder has the same name when ignoring lowercase/uppercase");
                            }
                            else
                            {
                                CrawlRemoteDocument(remoteDocument, remoteDocumentPath, localFolder, remoteFiles);
                                names.Add(remoteDocument.Name.ToLowerInvariant());
                            }
                        }
                        else if (isLink(cmisObject))
                        {
                            Logger.Debug("Ignoring file '" + remoteFolder + "/" + cmisObject.Name + "' of type '" +
                                cmisObject.ObjectType.Description + "'. Links are not currently handled.");
                        }
                        else
                        {
                            Logger.Warn("Unknown object type: '" + cmisObject.ObjectType.Description + "' (" + cmisObject.ObjectType.DisplayName
                                + ") for object " + remoteFolder + "/" + cmisObject.Name);
                        }
                    }
                    catch (CmisBaseException e)
                    {
                        ProcessRecoverableException("Could not access remote object: " + cmisObject.Name, e);
                        success = false;
                    }
                }
                return success;
            }

            private bool isLink(ICmisObject cmisObject)
            {
                IObjectType parent = cmisObject.ObjectType.GetParentType();
                while (parent != null)
                {
                    if (parent.Id.Equals("I:cm:link"))
                    {
                    return true;
                    }
                    parent = parent.GetParentType();
                }
                return false;
            }

            /// <summary>
            /// Crawl remote subfolder, syncing down if needed.
            /// Meanwhile, cache all contained remote folders, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private void CrawlRemoteFolder(IFolder remoteSubFolder, string remotePath, string localFolder, IList<string> remoteFolders)
            {
                SleepWhileSuspended();

                try
                {
                    if (Utils.WorthSyncing(localFolder, remoteSubFolder.Name, repoInfo))
                    {
                        Logger.Info("CrawlRemote localFolder:\"" + localFolder + "\" remoteSubFolder.Path:\"" + remoteSubFolder.Path + "\" remoteSubFolder.Name:\"" + remoteSubFolder.Name + "\"");
                        remoteFolders.Add(remoteSubFolder.Name);
                        var subFolderItem = database.GetFolderSyncItemFromRemotePath(remoteSubFolder.Path);
                        if (null == subFolderItem)
                        {
                            subFolderItem = SyncItemFactory.CreateFromRemoteFolder(remoteSubFolder.Path, repoInfo, database);
                        }

                        // Check whether local folder exists.
                        if (Directory.Exists(subFolderItem.LocalPath))
                        {
                            try
                            {
                            activityListener.ActivityStarted();
                            // Recurse into folder.
                            CrawlSync(remoteSubFolder, remotePath, subFolderItem.LocalPath);
                        }
                            finally
                            {
                                activityListener.ActivityStopped();
                            }
                        }
                        else
                        {
                            // Maybe the whole synchronized folder has disappeared?
                            // While rare for normal filesystems, that happens rather often with mounted folders (for instance encrypted folders)
                            // In such a case, we should abort this synchronization rather than delete the remote subfolder.
                            if (!Directory.Exists(repoInfo.TargetDirectory))
                            {
                                throw new Exception("Local folder has disappeared: " + repoInfo.TargetDirectory + " , aborting synchronization");
                            }

                            // If there was previously a file with this name, delete it.
                            // TODO warn if local changes in the file.
                            if (File.Exists(subFolderItem.LocalPath))
                            {
                                try
                                {
                                activityListener.ActivityStarted();
                                Utils.DeleteEvenIfReadOnly(subFolderItem.LocalPath);
                                }
                                finally
                                {
                                activityListener.ActivityStopped();
                            }
                            }

                            if (database.ContainsFolder(subFolderItem))
                            {
                                try
                                {
                                    activityListener.ActivityStarted();

                                // If there was previously a folder with this name, it means that
                                // the user has deleted it voluntarily, so delete it from server too.

                                    DeleteRemoteFolder(remoteSubFolder, subFolderItem, remotePath);
                                    }
                                finally
                                {
                                    activityListener.ActivityStopped();
                                }
                            }
                            else
                            {
                                try
                                {
                                    // Maybe the folder has been renamed
                                    // Check the id
                                    List<SyncItem> oldSyncItems = database.GetAllFoldersWithCmisId(remoteSubFolder.Id);
                                    if (oldSyncItems != null)
                                    {
                                        foreach (SyncItem item in oldSyncItems)
                                        {
                                            RemoveFolderLocally(item.LocalPath);
                                        }
                                    }

                                // The folder has been recently created on server, so download it.
                                activityListener.ActivityStarted();
                                Directory.CreateDirectory(subFolderItem.LocalPath);

                                // Create database entry for this folder.
                                // TODO - Yannick - Add metadata
                                database.AddFolder(subFolderItem, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                                Logger.Info("Added folder to database: " + subFolderItem.LocalPath);

                                // Recursive copy of the whole folder.
                                RecursiveFolderCopy(remoteSubFolder, remotePath, subFolderItem.LocalPath);

                                }
                                finally
                                {
                                activityListener.ActivityStopped();
                            }
                        }
                    }
                }
                }
                catch (Exception e)
                {
                    activityListener.ActivityStopped();
                    ProcessRecoverableException("Could not crawl sync remote folder: " + remoteSubFolder.Path, e);
                }
            }

            /// <summary>
            /// Check remote document, syncing down if needed.
            /// Meanwhile, cache remoteFiles, they are output parameters that are used in CrawlLocalFiles/CrawlLocalFolders
            /// </summary>
            private void CrawlRemoteDocument(IDocument remoteDocument, string remotePath, string localFolder, IList<string> remoteFiles)
            {
                SleepWhileSuspended();

                if (Utils.WorthSyncing(localFolder, repoInfo.CmisProfile.localFilename(remoteDocument), repoInfo))
                {
                    // We use the filename of the document's content stream.
                    // This can be different from the name of the document.
                    // For instance in FileNet it is not unusual to have a document where
                    // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                    string remoteDocumentFileName = repoInfo.CmisProfile.localFilename(remoteDocument);
                    //Logger.Debug("CrawlRemote doc: " + localFolder + CmisUtils.CMIS_FILE_SEPARATOR + remoteDocumentFileName);

                    // If this file does not have a filename, ignore it.
                    // It sometimes happen on IBM P8 CMIS server, not sure why.
                    if (remoteDocumentFileName == null)
                    {
                        Logger.Warn("Skipping download of '" + repoInfo.CmisProfile.localFilename(remoteDocument) + "' with null content stream in " + localFolder);
                        return;
                    }

                    if (remoteFiles != null)
                    {
                        remoteFiles.Add(remoteDocumentFileName);
                    }

                    var paths = remoteDocument.Paths;
                    var pathsCount = paths.Count;
                    var syncItem = database.GetSyncItemFromRemotePath(remotePath);
                    if (null == syncItem)
                    {
                        syncItem = SyncItemFactory.CreateFromRemoteDocument(remotePath, repoInfo.CmisProfile.localFilename(remoteDocument), repoInfo, database);
                    }

                    // Get HashCodes from the server
                    string hashDoc = remoteDocument.GetPropertyValue("sy:hashDoc") as string;
                    string hashMetadata = remoteDocument.GetPropertyValue("sy:hashDoc") as string;

                    if (syncItem.FileExistsLocal())
                    {
                        // Check modification date stored in database and download if remote modification date if different.
                        DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                        DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(syncItem);

                        if (lastDatabaseUpdate == null)
                        {
                            Logger.Info("Downloading file absent from database: " + syncItem.LocalPath);
                            activityListener.ActivityStarted();
                            DownloadFile(remoteDocument, remotePath, localFolder);
                            activityListener.ActivityStopped();
                        }
                        else
                        {
                            // If the file has been modified since last time we downloaded it, then download again.
                            if (serverSideModificationDate > lastDatabaseUpdate)
                            {
                                activityListener.ActivityStarted();

                                if (database.LocalFileHasChanged(syncItem.LocalPath))
                                {
                                    Logger.Info("Conflict with file: " + remoteDocumentFileName + ", backing up locally modified version and downloading server version");
                                    Logger.Info("- serverSideModificationDate: " + serverSideModificationDate);
                                    Logger.Info("- lastDatabaseUpdate: " + lastDatabaseUpdate);
                                    Logger.Info("- Checksum in database: " + database.GetFileChecksum(syncItem.LocalPath));
                                    Logger.Info("- Checksum of local file: " + Database.Database.Checksum(syncItem.LocalPath));

                                    // Rename locally modified file.
                                    String newFilePath = Utils.CreateConflictFilename(syncItem.LocalPath, repoInfo.User);
                                    File.Move(syncItem.LocalPath, newFilePath);

                                    // Download server version
                                    DownloadFile(remoteDocument, remotePath, localFolder);
                                    Logger.Info("- Checksum of remote file: " + Database.Database.Checksum(syncItem.LocalPath));
                                    repo.OnConflictResolved();

                                    // Notify the user.
                                    string lastModifiedBy = CmisUtils.GetProperty(remoteDocument, "cmis:lastModifiedBy");
                                    string message = String.Format(
                                        // Properties_Resources.ResourceManager.GetString("ModifiedSame", CultureInfo.CurrentCulture),
                                        "User {0} modified file \"{1}\" at the same time as you.",
                                        lastModifiedBy, syncItem.LocalPath)
                                        + "\n\n"
                                        // + Properties_Resources.ResourceManager.GetString("YourVersion", CultureInfo.CurrentCulture);
                                        + "Your version has been saved as \"" + newFilePath + "\", please merge your important changes from it and then delete it.";
                                    Logger.Info(message);
                                    Utils.NotifyUser(message);
                                }
                                else
                                {
                                    if (database.GetFileChecksum(syncItem.LocalPath) != hashDoc || hashDoc == null)
                                    {
                                        Logger.Info("Downloading modified file: " + remoteDocumentFileName);
                                        DownloadFile(remoteDocument, remotePath, localFolder);
                                    }
                                    else
                                    {
                                        // Only the metadatas have changed
                                        Logger.Info("Updating modified remote metadata: " + remoteDocumentFileName);
                                        CreateMetadataFile(syncItem);
                                        database.SetFileServerSideModificationDate(syncItem, serverSideModificationDate);
                                    }
                                }

                                activityListener.ActivityStopped();
                            }
                        }
                    }
                    else
                    {
                        // The remote file does not exist on the local filesystem.

                        // Maybe the whole synchronized folder has disappeared?
                        // While rare for normal filesystems, that happens rather often with mounted folders (for instance encrypted folders)
                        // In such a case, we should abort this synchronization rather than delete the remote file.
                        if ( ! Directory.Exists(repoInfo.TargetDirectory))
                        {
                            throw new Exception("Local target directory has disappeared: " + repoInfo.TargetDirectory + " , aborting synchronization");
                        }

                        if (database.ContainsLocalFile(syncItem.LocalRelativePath))
                        {
                            // The file used to be present locally (as revealed by the database), but does not exist anymore locally.
                            // So, it must have been deleted locally by the user.
                            // Thus, CmisSync must remove the file from the server too.

                            DeleteRemoteDocument(remoteDocument, syncItem);

                        }
                        else
                        {
                            // Maybe the file has been renamed
                            // Check if a file in the local folder with the remote document id exists
                            string id = Utils.RemoveVersion(remoteDocument.Id);
                            string filePath = database.GetFilePath(id);
                            if (filePath != null)
                            {
                                // File has been moved
                                SyncItem oldSynctItem = database.GetSyncItem(id);
                                string metadataFile = filePath + ".metadata";
                                
                                if (database.GetFileChecksum(syncItem.LocalPath) != hashDoc || hashDoc == null)
                                {
                                    // New content, so delete the old and download the new
                                    File.Delete(filePath);
                                    database.RemoveFile(oldSynctItem);
                                    if (metadataFile != null)
                                    {
                                        File.Delete(metadataFile);
                                        database.RemoveMetadataFile(oldSynctItem);
                                    }
                                    // New remote file, download it.
                                    Logger.Info("New remote file: " + syncItem.RemotePath);
                                    activityListener.ActivityStarted();
                                    DownloadFile(remoteDocument, remotePath, localFolder);
                                    activityListener.ActivityStopped();
                                }
                                else
                                {
                                    // File content has not change, just move it locally
                                    File.Move(filePath, syncItem.LocalPath);
                                    database.MoveFile(oldSynctItem, syncItem);

                                    // Update Metadatas
                                    File.Delete(metadataFile);
                                    database.RemoveMetadataFile(oldSynctItem);
                                    CreateMetadataFile(syncItem);
                                }
                            }
                        }
                        // New remote file, download it.
                        Logger.Info("New remote file: " + syncItem.RemotePath);
                        activityListener.ActivityStarted();
                        DownloadFile(remoteDocument, remotePath, localFolder);
                        activityListener.ActivityStopped();
                    }
                }
            }


            /// <summary>
            /// Crawl local files in a given directory (not recursive).
            /// </summary>
            private void CheckLocalFiles(string localFolder, IFolder remoteFolder, IList<string> remoteFiles)
            {
                SleepWhileSuspended();

                string[] files;
                try
                {
                    files = Directory.GetFiles(localFolder);
                }
                catch (Exception e)
                {
                    Logger.Warn("Could not get the file list from folder: " + localFolder, e);
                    return;
                }

                foreach (string filePath in files)
                {
                    CheckLocalFile(filePath, remoteFolder, remoteFiles);
                }
            }

            /// <summary>
            /// Check a local file in a given directory (not recursive).
            /// </summary>
            /// <param name="remoteFiles">Remove the file if it is not in this list of remote files. Ignored if null</param>
            private void CheckLocalFile(string filePath, IFolder remoteFolder, IList<string> remoteFiles)
            {
                SleepWhileSuspended();

                try
                {
                    if (Utils.IsSymlink(new FileInfo(filePath)))
                    {
                        Logger.Info("Skipping symbolic linked file: " + filePath);
                        return;
                    }
                    
                    var item = database.GetSyncItemFromLocalPath(filePath);
                    if (null == item)
                    {
                        // The file has been recently created locally (not synced from server).
                        item = SyncItemFactory.CreateFromLocalPath(filePath, false, repoInfo, database);
                    }

                    string fileName = item.RemoteLeafname;

                    if (Utils.WorthSyncing(Path.GetDirectoryName(filePath), fileName, repoInfo))
                    {
                        if (remoteFiles != null &&
                                ! ( remoteFiles.Contains(fileName) ||
                            // Workaround for Documentum which sometimes put a ".zip" extension to document names.
                            (CmisUtils.IsDocumentum(session) && remoteFiles.Contains(fileName + ".zip"))))
                        {
                            // This local file is not on the CMIS server now, so
                            // check whether it used to exist on server or not.
                            if (database.ContainsLocalFile(filePath))
                            {
                                if (database.LocalFileHasChanged(filePath))
                                {
                                    // If file has changed locally, move to 'your_version' and warn about conflict
                                    if (BIDIRECTIONAL)
                                    {
                                        // Use Extractor 
                                        try
                                        {
                                            //ConsoleApplication1.Program.ExtractMetaDatas(filePath);
                                        }
                                        catch(Exception e)
                                        {
                                            Logger.Error("Metadatas Extraction of " + filePath + " failed", e);
                                        }
                                        // Local file was updated, sync up.
                                        Logger.Info("Uploading locally edited remotely removed file from the repository: " + filePath);
                                        activityListener.ActivityStarted();
                                        UploadFile(filePath, remoteFolder);
                                        activityListener.ActivityStopped();
                                    }
                                    else
                                    {
                                        Logger.Info("Conflict with file: " + filePath + ", backing up locally modified version.");
                                        activityListener.ActivityStarted();
                                        // Rename locally modified file.
                                        String newFilePath = Utils.CreateConflictFilename(filePath, repoInfo.User);

                                        // The file might be ReadOnly, so make it writable first, otherwise the move will fail.
                                        File.SetAttributes(filePath, FileAttributes.Normal); // TODO use Utils.DeleteEvenIfReadOnly

                                        File.Move(filePath, newFilePath);

                                        // Delete file from database.
                                        database.RemoveFile(item);

                                        repo.OnConflictResolved();
                                        activityListener.ActivityStopped();
                                    }
                                }
                                else
                                {
                                    // File has been deleted on server, so delete it locally.
                                    Logger.Info("Removing remotely deleted file: " + filePath);
                                    activityListener.ActivityStarted();

                                    // The file might be ReadOnly, so make it writable first, otherwise removal will fail.
                                    File.SetAttributes(filePath, FileAttributes.Normal); // TODO use Utils.DeleteEvenIfReadOnly

                                    // Delete from the local filesystem.
                                    File.Delete(filePath);

                                    // Delete metadata file if exist
                                    string metadataFile = filePath + ".metadata";
                                    if (File.Exists(metadataFile))
                                    {
                                        File.Delete(metadataFile);
                                    }

                                    // Delete file from database.
                                    database.RemoveFile(item);
                                    // Delete metadat File from database
                                    database.RemoveMetadataFile(item);

                                    activityListener.ActivityStopped();
                                }
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // New file, sync up.
                                    Logger.Info("Uploading file absent on repository: " + filePath);
                                    activityListener.ActivityStarted();
                                    UploadFile(filePath, remoteFolder);
                                    activityListener.ActivityStopped();
                                }
                            }
                        }
                        else
                        {
                            // The file exists both on server and locally.
                            if (database.LocalFileHasChanged(filePath))
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // Upload new version of file content.
                                    Logger.Info("Uploading file update on repository: " + filePath);
                                    activityListener.ActivityStarted();
                                    UpdateFile(filePath, remoteFolder);

                                    // Update metadatas if necessary
                                    UpdateMetadata(filePath + ".metadata");

                                    activityListener.ActivityStopped();
                                }
                            }
                            else
                            {
                                // Update metadatas if necessary
                                UpdateMetadatasIfNecessary(item);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync local file: " + filePath, e);
                }
            }

            private void UpdateMetadatasIfNecessary(SyncItem item)
            {
                try
                {
                    string metadataFile = item.LocalPath + ".metadata";
                    if (!File.Exists(metadataFile))
                        return;
                    if (database.LocalMetadataHasChanged(metadataFile))
                    {
                        Logger.Info("Updating metadatas of " + item.LocalLeafname);
                        IDocument doc = session.GetObjectByPath(item.RemotePath) as IDocument;
                        if (doc == null)
                        {
                            return;
                        }
                        Dictionary<string, object> properties = PrepareCustomProperties(item);
                        doc.UpdateProperties(properties);
                        database.AddMetadataFile(item);
                        Logger.Info("metadatas of " + item.LocalLeafname + " updated");
                    }
                }
                catch(Exception e)
                {
                    ProcessRecoverableException("Cannot Update metadatas of " + item.LocalLeafname, e);
                }
            }


            /// <summary>
            /// Crawl local folders in a given directory (not recursive).
            /// </summary>
            private void CheckLocalFolders(string localFolder, IFolder remoteRoot, IList<string> remoteFolders)
            {
                SleepWhileSuspended();

                string[] folders;
                try
                {
                    folders = Directory.GetDirectories(localFolder);
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while get the folder list from folder {0}", localFolder), e);
                    return;
                }

                foreach (string localSubFolder in folders)
                {
                    CheckLocalFolder(localSubFolder, remoteRoot, remoteFolders);
                }
            }

            /// <summary>
            /// Check a particular local folder (not recursive).
            /// See whether it has been deleted locally or not.
            /// </summary>
            private void CheckLocalFolder(string localSubFolder, IFolder remoteRoot, IList<string> remoteFolders)
            {
                SleepWhileSuspended();
                try
                {
                    if (Utils.IsSymlink(new DirectoryInfo(localSubFolder)))
                    {
                        Logger.Info("Skipping symbolic link folder: " + localSubFolder);
                        return;
                    }

                    string folderName = Path.GetFileName(localSubFolder);

                    if (Utils.WorthSyncing(Path.GetDirectoryName(localSubFolder), folderName, repoInfo))
                    {
                    var syncFolderItem = database.GetFolderSyncItemFromLocalPath(localSubFolder);
                    if (null == syncFolderItem)
                    {
                            // The local item is not in database. It has not been uploaded yet.
                        syncFolderItem = SyncItemFactory.CreateFromLocalPath(localSubFolder, true, repoInfo, database);
                    }

                        if (remoteFolders.Contains(syncFolderItem.RemoteLeafname))
                        {
                            // The same folder exists locally and remotely.
                            // Are they synchronized, or were they just created (or moved) at the same time?
                            if (database.ContainsFolder(syncFolderItem))
                            {
                                // Check modification dates of local folder and remote folder.
                                // TODO
                            }
                            else
                    {
                                // The folder just appeared both on the local and remote sides.
                                // Rename local folder then download remote folder.

                                IFolder remoteFolder = (IFolder)session.GetObjectByPath(syncFolderItem.RemotePath);

                                var path = syncFolderItem.LocalPath;
                                var newPath = Utils.CreateConflictFoldername(path, repoInfo.User);

                                Directory.Move(path, newPath);

                                // Delete file from database.
                                database.RemoveFile(syncFolderItem);

                                repo.OnConflictResolved();

                                // Download folder from server.
                                DownloadDirectory(remoteFolder, syncFolderItem.RemotePath, path);
                            }
                        }
                        else
                        {
                            // This local folder is not on the CMIS server now, so
                            // check whether it used to exist on server or not.
                            if (database.ContainsFolder(syncFolderItem))
                            {
                                activityListener.ActivityStarted();
                                RemoveFolderLocally(localSubFolder);
                                activityListener.ActivityStopped();
                            }
                            else
                            {
                                    // New local folder, upload recursively.
                                    activityListener.ActivityStarted();
                                    UploadFolderRecursively(remoteRoot, localSubFolder);
                                    activityListener.ActivityStopped();
                                }
                            }
                        }

                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not crawl sync local folder: " + localSubFolder, e);
                }
            }
        }
    }
}