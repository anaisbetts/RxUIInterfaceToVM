interface IColorPicker : IRoutableViewModel {
	int Red { get; set; }
	int Green { get; set; }
	int Blue { get; set; }

	Color FinalColor { get; }
}

interface ILoginDialog : IRoutableViewModel {
	[Once]
	IAppState AppState { get; }

	string User { get; set; }
	string Password { get; set; }
	string PasswordAgain { get; set; }

	ReactiveCommand Ok { get; }
}
