import { Text } from 'react-native';
import Svg, { Path } from 'react-native-svg';
import { ICONS } from '../theme/icons';

// Draws a category's IconGlyph. An unknown key falls back to rendering the glyph
// as text, which is what a category still holding a literal emoji needs.
export function CategoryIcon({ iconKey, color, size = 20 }) {
  const icon = ICONS[iconKey];

  if (!icon) {
    return iconKey ? <Text style={{ fontSize: size * 0.9 }}>{iconKey}</Text> : null;
  }

  return (
    <Svg width={size} height={size} viewBox={`0 0 ${icon.viewBox} ${icon.viewBox}`}>
      <Path d={icon.d} fill={color} fillRule={icon.fillRule} clipRule={icon.fillRule} />
    </Svg>
  );
}
