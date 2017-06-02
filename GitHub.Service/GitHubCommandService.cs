using System;
using Octokit;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources.Core;
using Windows.ApplicationModel.VoiceCommands;
using System.Collections.Generic;

namespace GitHub.Service
{
    public sealed class GitHubCommandService : IBackgroundTask
    {
        VoiceCommandServiceConnection voiceServiceConnection;
        BackgroundTaskDeferral serviceDeferral;
        ResourceContext cortanaContext;
        VoiceCommandResponse response;
        GitHubClient client;

        #region Public Methos

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            System.Diagnostics.Debug.WriteLine("[GitHubCommandService][Run] Start");

            serviceDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnTaskCanceled;

            var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            cortanaContext = ResourceContext.GetForViewIndependentUse();

            // Initialize the GitHub Client
            client = new GitHubClient(new ProductHeaderValue("GitHub"));
            client.Credentials = new Credentials("[EMAIL]", "[PASSWORD]");

            if (triggerDetails != null && triggerDetails.Name == "GitHubCommandService")
            {
                try
                {
                    voiceServiceConnection =  
                        VoiceCommandServiceConnection.FromAppServiceTriggerDetails(
                            triggerDetails);

                    voiceServiceConnection.VoiceCommandCompleted += OnVoiceCommandCompleted;
                    VoiceCommand voiceCommand = await voiceServiceConnection.GetVoiceCommandAsync();

                    switch (voiceCommand.CommandName)
                    {
                        case "checkNotifies":     
                            await CheckNotifies();
                            break;
                        case "checkRepositories":
                            await CheckRepositories();
                            break;
                        default:
                            // Handle the possibility that the user has removed
                            // a voice command that is still registered.
                            LaunchAppInForeground();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Handling Voice Command failed " + ex.ToString());
                }
            }

            System.Diagnostics.Debug.WriteLine("[GitHubCommandService][Run] End");
        }

        #endregion

        #region Private Events

        /// <summary>
        /// Provide a simple response that launches the app.
        /// </summary>
        private async void LaunchAppInForeground()
        {
            var userMessage = new VoiceCommandUserMessage();
            userMessage.SpokenMessage = "Launching GitHub";

            var response = VoiceCommandResponse.CreateResponse(userMessage);

            response.AppLaunchArgument = "";

            await voiceServiceConnection.RequestAppLaunchAsync(response);
        }

        /// <summary>
        /// When the background task is cancelled, clean up/cancel any ongoing long-running operations.
        /// </summary>
        /// <param name="sender">This background task instance</param>
        /// <param name="reason">Contains an enumeration with the reason for task cancellation</param>
        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            System.Diagnostics.Debug.WriteLine("Task cancelled, clean up");
            if (this.serviceDeferral != null)
            {
                //Complete the service deferral
                this.serviceDeferral.Complete();
            }
        }

        /// <summary>
        /// Handle the completion of the voice command.
        /// </summary>
        /// <param name="sender">The voice connection associated with the command.</param>
        /// <param name="args">Contains an Enumeration indicating why the command was terminated.</param>
        private void OnVoiceCommandCompleted(VoiceCommandServiceConnection sender, VoiceCommandCompletedEventArgs args)
        {
            if (this.serviceDeferral != null)
            {
                this.serviceDeferral.Complete();
            }
        }

        #endregion

        #region  Private Methods

        private async Task CheckNotifies()
        {
            System.Diagnostics.Debug.WriteLine("[GitHubService][CheckNotifies] Start");

            //Declare the variables responsable for the responses
            var userReprompt = new VoiceCommandUserMessage();
            var userPrompt = new VoiceCommandUserMessage();

            try
            {

                // If this operation is expected to take longer than 0.5 seconds
                // the task provide a progress response
                string loadingTripToDestination = "I'm still checking, please wait";
                await ShowProgressScreen(loadingTripToDestination);

                //Get all notification of the current logged user
                var notifications = await client.Activity.Notifications.GetAllForCurrent();

                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckNotifies] notifications: " + notifications);
                if (notifications.Count == 0)
                {
                    // Create a re-prompt message if the user responds with an out-of-grammar response.
                    userPrompt.DisplayMessage = userPrompt.SpokenMessage = "You haven't any notification.";
                    userReprompt.DisplayMessage = userReprompt.SpokenMessage = "You haven't any notification.";
                    response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt);

                    await voiceServiceConnection.ReportSuccessAsync(response);

                }
                if (notifications.Count > 0)
                {
                    // Create a re-prompt message if the user responds with an out-of-grammar response.
                    userPrompt.DisplayMessage = userPrompt.SpokenMessage = "I have found more than one notification, do you wanna see the other?";
                    userReprompt.DisplayMessage = userReprompt.SpokenMessage = "Come on dude!... Do you wanna see the others or not?";

                    // Cortana will handle re-prompting if the user does not provide a valid response.
                    response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt);
                    var userResponse = await voiceServiceConnection.RequestConfirmationAsync(response);

                    if (userResponse.Confirmed)
                    {
                        // Show all the notifications
                        var contentTiles = new List<VoiceCommandContentTile>();
                        foreach (Notification n in notifications)
                        {
                            contentTiles.Add(new VoiceCommandContentTile()
                            {
                                TextLine1 = n.Subject.ToString(),
                                TextLine2 = n.Repository.ToString()
                            });

                            response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt, contentTiles);
                            var userResponseDisambiguos = await voiceServiceConnection.RequestDisambiguationAsync(response);
                        }

                    }
                    else
                    {
                        // Close the comunication
                        userPrompt.DisplayMessage = userPrompt.SpokenMessage = "See you nerd!";
                        response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt);

                        await voiceServiceConnection.ReportSuccessAsync(response);
                    }
                }
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckNotifies] End");
            }
            catch (Exception exc)
            {
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckNotifies] Error: " + exc.Message);
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckNotifies] End");
            }
        }

        private async Task CheckRepositories()
        {
            System.Diagnostics.Debug.WriteLine("[GitHubService][CheckRepositories] Start");

            //Declare the variables responsable for the responses
            var userReprompt = new VoiceCommandUserMessage();
            var userPrompt = new VoiceCommandUserMessage();

            try
            {
                // If this operation is expected to take longer than 0.5 seconds
                // the task provide a progress response
                string loadingTripToDestination = "I'm still checking, please wait";
                await ShowProgressScreen(loadingTripToDestination);

                //Get all notification of the current logged user
                var repositories = await client.Repository.GetAllForCurrent();

                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckRepositories] repositories.count: " + repositories.Count);
                if (repositories.Count == 0)
                {
                    // Create a re-prompt message if the user responds with an out-of-grammar response.
                    userPrompt.DisplayMessage = userPrompt.SpokenMessage = "You haven't any repository.";
                    userReprompt.DisplayMessage = userReprompt.SpokenMessage = "Dude...no one want to talk to you! You haven't any repository.";
                    response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt);

                    await voiceServiceConnection.ReportSuccessAsync(response);
                }
                if (repositories.Count > 0)
                {
                    userPrompt.DisplayMessage = userPrompt.SpokenMessage = "Your repositories are: ";
                    userReprompt.DisplayMessage = userReprompt.SpokenMessage = "Dude...do you speak english? These are you repository";

                    // Show all the repositories
                    var contentTiles = new List<VoiceCommandContentTile>();
                    foreach (Repository r in repositories)
                    {
                        contentTiles.Add(new VoiceCommandContentTile()
                        {
                            Title = r.Name.ToString(),
                            ContentTileType = VoiceCommandContentTileType.TitleWithText,
                            TextLine1 = r.FullName.ToString(),
                            TextLine2 = string.Format("Updated at {0}", r.UpdatedAt.ToString("yyyy-mm-dd"))
                            //TextLine3 = string.Format("Updated from {0}", "N:D") // To get this value i have to call the commits and get the last one
                        });
                    }

                    response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt, contentTiles);
                    var userResponseDisambiguos = await voiceServiceConnection.RequestDisambiguationAsync(response);

                    // Get the last commit in the selected repository
                    var selectedRepository = repositories.Single(r => r.Name.Equals(userResponseDisambiguos.SelectedItem.Title, StringComparison.CurrentCulture));
                    CommitRequest request = new CommitRequest()
                    {
                        Since = selectedRepository.UpdatedAt
                    };
                    IReadOnlyList<GitHubCommit> lastCommit = await client.Repository.Commit.GetAll(selectedRepository.Id, request);
                    var commitInfo = lastCommit.First().Commit;
                    // Show the informations about the last commit 
                    var text = string.Format("The repository {0} was updated on {1} from {2} and the description is: '{3}'",
                        selectedRepository.Name,
                        commitInfo.Author.Name,
                        selectedRepository.UpdatedAt.ToString("yyyy-mm-dd"),
                        commitInfo.Message);

                    userPrompt.DisplayMessage = userPrompt.SpokenMessage = text;
                    userReprompt.DisplayMessage = userReprompt.SpokenMessage = "ACDC Rocks";
                    response = VoiceCommandResponse.CreateResponseForPrompt(userPrompt, userReprompt);

                    await voiceServiceConnection.ReportSuccessAsync(response);

                }
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckRepositories] End");
            }
            catch (Exception exc)
            {
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckRepositories] Error: " + exc.Message);
                System.Diagnostics.Debug.WriteLine("[GitHubService][CheckRepositories] End");
            }
        }

        /// <summary>
        /// Show a progress screen.
        /// </summary>
        /// <param name="message">The message to display, relating to the task being performed.</param>
        /// <returns></returns>
        private async Task ShowProgressScreen(string message)
        {
            var userProgressMessage = new VoiceCommandUserMessage();
            userProgressMessage.DisplayMessage = userProgressMessage.SpokenMessage = message;

            VoiceCommandResponse response = VoiceCommandResponse.CreateResponse(userProgressMessage);
            await voiceServiceConnection.ReportProgressAsync(response);
        }

        #endregion


    }
}
