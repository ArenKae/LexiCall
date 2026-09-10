import Svg, { G, Path } from 'react-native-svg';

// Expander arrow for the category tree: the desktop client's stroked chevron,
// rotated a quarter turn once the node is open.
export function TreeChevron({ expanded, color, size = 16 }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 16 16">
      <G rotation={expanded ? 90 : 0} origin="7.5, 7">
        <Path
          d="M 5 2 L 10 7 L 5 12"
          stroke={color}
          strokeWidth={1.6}
          strokeLinejoin="round"
          strokeLinecap="round"
          fill="none"
        />
      </G>
    </Svg>
  );
}
