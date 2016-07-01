﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Famoser.FrameworkEssentials.Logging;
using Famoser.SqliteWrapper.Repositories;
using Famoser.SqliteWrapper.Services.Interfaces;
using OfflineMedia.Business.Enums.Models;
using OfflineMedia.Business.Models;
using OfflineMedia.Business.Models.NewsModel;
using OfflineMedia.Business.Models.NewsModel.ContentModels;
using OfflineMedia.Data.Entities.Database.Contents;

namespace OfflineMedia.Business.Services
{
    public class ImageDownloadService
    {
        private readonly IPlatformCodeService _platformCodeService;
        private readonly ISqliteService _sqliteService;
        private readonly GenericRepository<ImageContentModel, ImageContentEntity> _genericRepository;

        public ImageDownloadService(IPlatformCodeService platformCodeService, ISqliteService sqliteService)
        {
            _platformCodeService = platformCodeService;
            _sqliteService = sqliteService;

            _genericRepository = new GenericRepository<ImageContentModel, ImageContentEntity>(sqliteService);
        }

        private static readonly ConcurrentQueue<ImageContentModel> PriorityImages = new ConcurrentQueue<ImageContentModel>();
        private static readonly ConcurrentQueue<ImageContentModel> SecondaryImages = new ConcurrentQueue<ImageContentModel>();
        private static readonly ConcurrentBag<Task> ActiveTasks = new ConcurrentBag<Task>();

        private const int MaxActiveTasks = 5;

        public void Download(ImageContentModel imageContentModel, bool priority = false)
        {
            if (priority)
            {
                if (!PriorityImages.Contains(imageContentModel))
                {
                    PriorityImages.Enqueue(imageContentModel);
                    StartThreadIfNecessary();
                }
            }
            else if (!SecondaryImages.Contains(imageContentModel))
            {
                SecondaryImages.Enqueue(imageContentModel);
                StartThreadIfNecessary();
            }
        }

        public void Download(FeedModel model)
        {
            foreach (var articleModel in model.ArticleList)
            {
                if (articleModel.LeadImage != null)
                    Download(articleModel.LeadImage, true);
            }
        }

        public void Download(ArticleModel model)
        {
            if (model.LeadImage != null && model.LeadImage.LoadingState == LoadingState.New && !PriorityImages.Contains(model.LeadImage))
                Download(model.LeadImage, true);

            foreach (var baseContentModel in model.Content)
            {
                if (baseContentModel is ImageContentModel)
                {
                    Download((ImageContentModel) baseContentModel);
                }
                else if (baseContentModel is GalleryContentModel)
                {
                    foreach (var imageContentModel in ((GalleryContentModel)baseContentModel).Images)
                    {
                        Download(imageContentModel);
                    }
                }
            }
        }

        private void StartThreadIfNecessary()
        {
            if (PriorityImages.Any() || SecondaryImages.Any())
            {
                //might lead to too much tasks in concurrent setups, but does not really matter in this case
                for (int i = ActiveTasks.Count; i < MaxActiveTasks && i < ActiveTasks.Count; i = ActiveTasks.Count)
                {
                    ActiveTasks.Add(DownloadImagesTask());
                }
            }
        }

        private async Task DownloadImagesTask()
        {
            try
            {
                ImageContentModel model;
                while (PriorityImages.TryDequeue(out model))
                {
                    model.Image = await _platformCodeService.DownloadResizeImage(new Uri(model.Url));
                    await _genericRepository.SaveAsyc(model);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Instance.LogException(ex);
                throw;
            }
        }
    }
}
