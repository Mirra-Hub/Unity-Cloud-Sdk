using MirraCloud.Core.PromoCodes.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.PromoCodes
{
    /// <summary>
    /// Client for the PromoCodes SDK endpoints (<c>api/cloud/sdk/promo-codes/v1</c>).
    /// </summary>
    public class PromoCodesService : ICloudSdkService
    {
        private const string ControllerApi = "/promo-codes/v1";

        private readonly RestApiClient _restApi;
        private readonly Configuration _configuration;

        public PromoCodesService(Configuration configuration, RestApiClient restApi)
        {
            _configuration = configuration;
            _restApi = restApi;
        }

        /// <summary>Redeem a promo code. Returns granted rewards/effects and a structured status.</summary>
        public AsyncOperation<RestApiResult<RedeemPromoCodeResponseDto>> RedeemAsync(string code)
        {
            string route = $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/redeem";
            var dto = new RedeemPromoCodeRequestDto { code = code };
            return _restApi.PostAsync<RedeemPromoCodeResponseDto>(route, dto);
        }

        /// <summary>Recent redemption history of the current player.</summary>
        public AsyncOperation<RestApiResult<PromoHistoryItemDto[]>> GetHistoryAsync(int limit = 50)
        {
            string route =
                $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/history?limit={limit}";
            return _restApi.GetAsync<PromoHistoryItemDto[]>(route);
        }

        /// <summary>Active (non-expired) promo effects on the current player.</summary>
        public AsyncOperation<RestApiResult<PromoActiveEffectDto[]>> GetActiveEffectsAsync()
        {
            string route =
                $"{ControllerApi}/projects/{_configuration.ProjectId}/branches/{_configuration.BranchId}/active-effects";
            return _restApi.GetAsync<PromoActiveEffectDto[]>(route);
        }

        public void CloudSdkInitialize() { }
        public void CloudSdkDispose() { }
    }
}
