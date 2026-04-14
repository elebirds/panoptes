// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 提供 Panoptes 服务端进程入口。

package main

import "github.com/elebirds/panoptes/cmd/server/app"

func main() {
	a := app.New()
	a.Run()
}
