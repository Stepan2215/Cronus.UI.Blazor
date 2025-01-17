﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Blazored.LocalStorage;
//using Blazor.Extensions.Storage;
using Elders.Cronus.Dashboard.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Dashboard.Components
{
    public class ConnectionBase : ComponentBase
    {
        public ConnectionBase()
        {
            TenantAuths = new List<oAuth>();
        }

        [Inject]
        protected AppState App { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected ILogger<ConnectionBase> Log { get; set; }

        [Inject]
        protected ILocalStorageService LocalStorage { get; set; }

        [Inject]
        protected TokenClient Token { get; set; }

        [Inject]
        protected HttpClient HttpClient { get; set; }

        [Inject]
        protected CronusClient Cronus { get; set; }

        [Parameter]
        public string Name { get; set; }

        [Parameter]
        public string CronusEndpoint { get; set; }

        [Parameter]
        public string oAuthEndpoint { get; set; }

        [Parameter]
        public string oAuthClient { get; set; }

        [Parameter]
        public string oAuthSecret { get; set; }

        [Parameter]
        public List<oAuth> TenantAuths { get; set; }

        [Parameter]
        public bool IsEndpointValid { get; set; }

        public string AccessToken = String.Empty;

        protected override async Task OnInitializedAsync()
        {
            await LoadDataAsync();
        }

        private const string PathToConnections = "/connections";
        protected List<Connection> connections;
        protected Connection connection;

        protected async Task<bool> LoadDataAsync()
        {
            connections = await LocalStorage.GetItemAsync<List<Connection>>(LSKey.Connections);

            if (string.IsNullOrEmpty(Name) == false)
            {
                connection = connections.Where(conn => conn.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)).SingleOrDefault();

                Name = connection.Name;
                CronusEndpoint = connection.CronusEndpoint;
                oAuthEndpoint = connection.oAuth.ServerEndpoint;
                oAuthClient = connection.oAuth.Client;
                oAuthSecret = connection.oAuth.Secret;
                TenantAuths = connection.oAuths;
            }

            await GetTenants();

            return true;
        }

        protected async Task EditConnection()
        {
            if (connections.Remove(connection))
            {
                var newConnection = GetConnection();
                connections.Add(newConnection);
                await LocalStorage.SetItemAsync(LSKey.Connections, connections);
                connection = newConnection;

                App.UpdateConnections(connections);
            }

            StateHasChanged();
        }

        protected async Task AddConnection()
        {
            var connections = await LocalStorage.GetItemAsync<List<Connection>>(LSKey.Connections) ?? new List<Connection>();

            var newConnection = GetConnection();
            connections.Add(newConnection);
            await LocalStorage.SetItemAsync(LSKey.Connections, connections);

            App.UpdateConnections(connections);

            StateHasChanged();
            NavigationManager.NavigateTo(PathToConnections);
        }

        Connection GetConnection()
        {
            return new Connection(Name, CronusEndpoint)
            {
                Name = Name,
                CronusEndpoint = CronusEndpoint,
                oAuth = new oAuth()
                {
                    ServerEndpoint = oAuthEndpoint,
                    Client = oAuthClient,
                    Secret = oAuthSecret
                },
                oAuths = TenantAuths
            };
        }

        protected async Task CheckConnection()
        {
            List<string> response = await Cronus.GetTenantsAsync(new Connection(Name, CronusEndpoint));

            IsEndpointValid = response.Any() ? true : false;
            StateHasChanged();
        }

        protected async Task GetToken(oAuth oAuth)
        {
            Log.LogDebug("GetToken()");

            Connection testConnection = new Connection(Name, CronusEndpoint, oAuth);
            AccessToken = await Token.GetAccessTokenAsync(testConnection);

            Log.LogDebug(AccessToken);
        }

        protected async Task GetTenants()
        {
            Log.LogDebug("GetTenants()");

            var tenantsForCurrentConnection = await Cronus.GetTenantsAsync(new Connection(Name, CronusEndpoint));
            IsEndpointValid = tenantsForCurrentConnection.Any() ? true : false;

            foreach (var tenant in tenantsForCurrentConnection)
            {
                bool tenantHasAlreadyBeenSetUp = TenantAuths.Any(x => x.Tenant.Equals(tenant));
                if (tenantHasAlreadyBeenSetUp)
                    continue;

                TenantAuths.Add(new oAuth()
                {
                    Tenant = tenant,
                    ServerEndpoint = oAuthEndpoint
                });
            }

            StateHasChanged();
        }

        protected async Task OnDelete(Connection model)
        {
            Log.LogDebug("OnDelete");

            connections.Remove(model);
            await LocalStorage.SetItemAsync(LSKey.Connections, connections);

            StateHasChanged();
        }
    }
}
