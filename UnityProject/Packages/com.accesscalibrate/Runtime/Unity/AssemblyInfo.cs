// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

// Lets the package's own tests reset the static API between tests.
[assembly: InternalsVisibleTo("AccessCalibrate.Runtime.Tests")]
[assembly: InternalsVisibleTo("AccessCalibrate.PlayMode.Tests")]
[assembly: InternalsVisibleTo("AccessCalibrate.Demo.Tests")]
