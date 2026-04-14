// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的错误类型与错误码映射。

package game

import "errors"

var (
	ErrPhaseMismatch = errors.New("phase_mismatch")
)
