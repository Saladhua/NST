// 可拖拽列宽的表头单元格：配合 antd Table 的 components.header.cell 使用。
// 参考 antd 官方「拖拽表头调整列宽」示例实现。
import type { HTMLAttributes, ReactNode, SyntheticEvent } from 'react';
import { Resizable } from 'react-resizable';
import type { ResizeCallbackData } from 'react-resizable';

export interface ResizableTitleProps extends HTMLAttributes<HTMLTableCellElement> {
  /** 当前列宽（无宽度时按普通标题渲染，不参与拖拽）。 */
  width: number;
  /** 拖拽最小宽度。 */
  minWidth?: number;
  /** 拖拽回调：e 为鼠标事件，data.size.width 为实时宽度。 */
  onResize: (e: SyntheticEvent, data: ResizeCallbackData) => void;
  children?: ReactNode;
}

export default function ResizableTitle({
  width,
  minWidth = 80,
  onResize,
  children,
  ...restProps
}: ResizableTitleProps) {
  if (!width) {
    return <th {...restProps}>{children}</th>;
  }

  return (
    <Resizable
      width={width}
      height={0}
      minConstraints={[minWidth, 0]}
      onResize={onResize}
      draggableOpts={{ enableUserSelectHack: false }}
    >
      <th {...restProps} style={{ ...restProps.style, overflow: 'visible' }}>
        {children}
      </th>
    </Resizable>
  );
}