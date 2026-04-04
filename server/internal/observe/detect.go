package observe

import (
	"io"
	"os"

	"github.com/mattn/go-isatty"
)

// IsTerminal 检测 writer 是否连接到终端。
func IsTerminal(w io.Writer) bool {
	file, ok := w.(*os.File)
	if !ok {
		return false
	}

	fd := file.Fd()
	return isatty.IsTerminal(fd) || isatty.IsCygwinTerminal(fd)
}

// isTerminal 是 IsTerminal 的内部别名，避免在 logger.go 中重复检测逻辑。
func isTerminal(w io.Writer) bool {
	return IsTerminal(w)
}
