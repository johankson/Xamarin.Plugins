//
//  Copyright 2011-2013, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Plugin.Media.Abstractions;
using Plugin.Permissions;

namespace Plugin.Media
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    [Android.Runtime.Preserve(AllMembers = true)]
    public class MediaImplementation : IMedia
    {
        /// <summary>
        /// Implementation
        /// </summary>
        public MediaImplementation()
        {

            this.context = Android.App.Application.Context;
            IsCameraAvailable = context.PackageManager.HasSystemFeature(PackageManager.FeatureCamera);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Gingerbread)
                IsCameraAvailable |= context.PackageManager.HasSystemFeature(PackageManager.FeatureCameraFront);
        }

        ///<inheritdoc/>
        public Task<bool> Initialize()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public bool IsCameraAvailable
        {
            get;
            private set;
        }
        /// <inheritdoc/>
        public bool IsTakePhotoSupported
        {
            get { return true; }
        }
        /// <inheritdoc/>
        public bool IsPickPhotoSupported
        {
            get { return true; }
        }
        /// <inheritdoc/>
        public bool IsTakeVideoSupported
        {
            get { return true; }
        }
        /// <inheritdoc/>
        public bool IsPickVideoSupported
        {
            get { return true; }
        }

        public Task CrossPermission { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Intent GetPickPhotoUI()
        {
            int id = GetRequestId();
            return CreateMediaIntent(id, "image/*", Intent.ActionPick, null, tasked: false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public Intent GetTakePhotoUI(StoreCameraMediaOptions options)
        {
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            VerifyOptions(options);

            int id = GetRequestId();
            return CreateMediaIntent(id, "image/*", MediaStore.ActionImageCapture, options, tasked: false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Intent GetPickVideoUI()
        {
            int id = GetRequestId();
            return CreateMediaIntent(id, "video/*", Intent.ActionPick, null, tasked: false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public Intent GetTakeVideoUI(StoreVideoOptions options)
        {
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            VerifyOptions(options);

            return CreateMediaIntent(GetRequestId(), "video/*", MediaStore.ActionVideoCapture, options, tasked: false);
        }

        /// <summary>
        /// Picks a photo from the default gallery
        /// </summary>
        /// <returns>Media file or null if canceled</returns>
        public async Task<MediaFile> PickPhotoAsync()
        {
            if (!(await RequestStoragePermission().ConfigureAwait(false)))
            {
                return null;
            }
            return await TakeMediaAsync("image/*", Intent.ActionPick, null);
        }


        async Task<bool> RequestCameraPermission()
        {
            var status1 = await CrossPermissions.Current.CheckPermissionStatusAsync(Permissions.Abstractions.Permission.Storage);
            var status2 = await CrossPermissions.Current.CheckPermissionStatusAsync(Permissions.Abstractions.Permission.Camera);
            if (status1 != Permissions.Abstractions.PermissionStatus.Granted ||
                status2 != Permissions.Abstractions.PermissionStatus.Granted)
            {
                Console.WriteLine("Does not have storage or camera permissions granted, requesting.");
                var results = await CrossPermissions.Current.RequestPermissionsAsync(Permissions.Abstractions.Permission.Camera, Permissions.Abstractions.Permission.Storage);
                if (results[Permissions.Abstractions.Permission.Storage] != Permissions.Abstractions.PermissionStatus.Granted ||
                    results[Permissions.Abstractions.Permission.Camera] != Permissions.Abstractions.PermissionStatus.Granted)
                {
                    Console.WriteLine("Storage or Camera permission Denied.");
                    return false;
                }
            }

            return true;
        }

        async Task<bool> RequestStoragePermission()
        {
            var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permissions.Abstractions.Permission.Storage);
            if (status != Permissions.Abstractions.PermissionStatus.Granted)
            {
                Console.WriteLine("Does not have storage permissions granted, requesting.");
                var results = await CrossPermissions.Current.RequestPermissionsAsync(Permissions.Abstractions.Permission.Storage);
                if (results[Permissions.Abstractions.Permission.Storage] != Permissions.Abstractions.PermissionStatus.Granted)
                {
                    Console.WriteLine("Storage permission Denied.");
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Take a photo async with specified options
        /// </summary>
        /// <param name="options">Camera Media Options</param>
        /// <returns>Media file of photo or null if canceled</returns>
        public async Task<MediaFile> TakePhotoAsync(StoreCameraMediaOptions options)
        {
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            if (!(await RequestCameraPermission().ConfigureAwait(false)))
            {
                return null;
            }


            VerifyOptions(options);

            return await TakeMediaAsync("image/*", MediaStore.ActionImageCapture, options);
        }

        /// <summary>
        /// Picks a video from the default gallery
        /// </summary>
        /// <returns>Media file of video or null if canceled</returns>
        public async Task<MediaFile> PickVideoAsync()
        {

            if (!(await RequestStoragePermission().ConfigureAwait(false)))
            {
                return null;
            }

            return await TakeMediaAsync("video/*", Intent.ActionPick, null);
        }

        /// <summary>
        /// Take a video with specified options
        /// </summary>
        /// <param name="options">Video Media Options</param>
        /// <returns>Media file of new video or null if canceled</returns>
        public async Task<MediaFile> TakeVideoAsync(StoreVideoOptions options)
        {
            if (!IsCameraAvailable)
                throw new NotSupportedException();

            if (!(await RequestCameraPermission().ConfigureAwait(false)))
            {
                return null;
            }

            VerifyOptions(options);

            return await TakeMediaAsync("video/*", MediaStore.ActionVideoCapture, options);
        }

        private readonly Context context;
        private int requestId;
        private TaskCompletionSource<Plugin.Media.Abstractions.MediaFile> completionSource;

        private void VerifyOptions(StoreMediaOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (Path.IsPathRooted(options.Directory))
                throw new ArgumentException("options.Directory must be a relative path", "options");
        }

        private Intent CreateMediaIntent(int id, string type, string action, StoreMediaOptions options, bool tasked = true)
        {
            Intent pickerIntent = new Intent(this.context, typeof(MediaPickerActivity));
            pickerIntent.PutExtra(MediaPickerActivity.ExtraId, id);
            pickerIntent.PutExtra(MediaPickerActivity.ExtraType, type);
            pickerIntent.PutExtra(MediaPickerActivity.ExtraAction, action);
            pickerIntent.PutExtra(MediaPickerActivity.ExtraTasked, tasked);

            if (options != null)
            {
                pickerIntent.PutExtra(MediaPickerActivity.ExtraPath, options.Directory);
                pickerIntent.PutExtra(MediaStore.Images.ImageColumns.Title, options.Name);


                var cameraOptions = (options as StoreCameraMediaOptions);
                if (cameraOptions != null)
                    pickerIntent.PutExtra(MediaPickerActivity.ExtraSaveToAlbum, cameraOptions.SaveToAlbum);

                var vidOptions = (options as StoreVideoOptions);
                if (vidOptions != null)
                {
                    pickerIntent.PutExtra(MediaStore.ExtraDurationLimit, (int)vidOptions.DesiredLength.TotalSeconds);
                    pickerIntent.PutExtra(MediaStore.ExtraVideoQuality, (int)vidOptions.Quality);
                }
            }
            //pickerIntent.SetFlags(ActivityFlags.ClearTop);
            pickerIntent.SetFlags(ActivityFlags.NewTask);
            return pickerIntent;
        }

        private int GetRequestId()
        {
            int id = this.requestId;
            if (this.requestId == Int32.MaxValue)
                this.requestId = 0;
            else
                this.requestId++;

            return id;
        }

        private Task<Plugin.Media.Abstractions.MediaFile> TakeMediaAsync(string type, string action, StoreMediaOptions options)
        {
            int id = GetRequestId();

            var ntcs = new TaskCompletionSource<Plugin.Media.Abstractions.MediaFile>(id);
            if (Interlocked.CompareExchange(ref this.completionSource, ntcs, null) != null)
                throw new InvalidOperationException("Only one operation can be active at a time");

            this.context.StartActivity(CreateMediaIntent(id, type, action, options));

            EventHandler<MediaPickedEventArgs> handler = null;
            handler = (s, e) =>
            {
                var tcs = Interlocked.Exchange(ref this.completionSource, null);

                MediaPickerActivity.MediaPicked -= handler;

                if (e.RequestId != id)
                    return;

                if (e.Error != null)
                    tcs.SetResult(null);
                else if (e.IsCanceled)
                    tcs.SetResult(null);
                else
                    tcs.SetResult(e.Media);
            };

            MediaPickerActivity.MediaPicked += handler;

            return ntcs.Task;
        }
    }
}