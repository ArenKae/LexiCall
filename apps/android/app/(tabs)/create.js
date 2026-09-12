import { Redirect } from 'expo-router';

// Never actually shown: the tab bar intercepts this tab's press and pushes
// /entry/edit instead. This redirect only covers a stray deep link landing
// here directly.
export default function CreateTabPlaceholder() {
  return <Redirect href="/entry/edit" />;
}
