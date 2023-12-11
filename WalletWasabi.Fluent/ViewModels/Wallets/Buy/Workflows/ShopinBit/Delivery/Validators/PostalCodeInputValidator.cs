using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PostalCodeInputValidator : TextInputInputValidator
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;

	public PostalCodeInputValidator(
		WorkflowState workflowState,
		DeliveryWorkflowRequest deliveryWorkflowRequest,
		ChatMessageMetaData.ChatMessageTag tag)
		: base(workflowState, null, "Type here...", tag: tag)
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;

		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return !string.IsNullOrWhiteSpace(Message);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			var message = Message;

			_deliveryWorkflowRequest.PostalCode = message;

			return message;
		}

		return null;
	}
}
