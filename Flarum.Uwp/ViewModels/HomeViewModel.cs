﻿using CommunityToolkit.Mvvm.ComponentModel;
using Flarum.Api.ApiContracts;
using Flarum.Api.Bases;
using Flarum.Desktop.Contracts.ViewModels;
using Flarum.Provider;
using Flarum.Provider.Mappers;
using Flarum.Provider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flarum.ViewModels
{
    public partial class HomeViewModel : ObservableRecipient, IViewModel
    {
        private readonly FlarumProvider flarumProvider;

        [ObservableProperty] private List<FlarumDiscussion> _discussions;
        [ObservableProperty] private FlarumForum _forum;

        public HomeViewModel()
        {
            flarumProvider = Locator.Instance.GetService<FlarumProvider>();
        }

        public async Task GetDataAsync()
        {
            Forum = App.CurrentForum;

            var result = await flarumProvider.RequestAsync<GetAllDiscussionsRequest, GetAllDiscussionsResponse, ErrorResultBase, GetAllDiscussionsActualRequest>(new GetAllDiscussionsApi());
            
            Discussions = result.Match(
                success => success?.Discussions?.Select(t=>(FlarumDiscussion)DiscussionDataToFlarumDiscussionMapper.MapToFlarumDiscussion(t.FlarumDiscussion)).ToList() ?? new List<FlarumDiscussion>(),
                error=>new List<FlarumDiscussion>());
        }
    }
}
