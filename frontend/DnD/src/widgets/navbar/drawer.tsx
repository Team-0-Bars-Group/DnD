import Box from '@mui/material/Box';
import {Drawer as MaterialDrawer} from '@mui/material';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import { useNavigate } from 'react-router-dom';

interface DrawerProps {
    close: () => void
    isOpen: boolean
}

export default function Drawer({ isOpen, close }: DrawerProps) {
  const navigate = useNavigate();
  const buttons = [
    {
        title: "Герои",
        navigationLink: "/my-characters"
    },
  ]

  const onButtonClick = (link: string) => {
    navigate(link);
  };
    
  return (
    <div>
      <MaterialDrawer open={isOpen} onClose={close}>
        <Box sx={{ width: 250 }} role="presentation" onClick={close}>
            <List>
            {buttons.map((info) => (
            <ListItem key={info.title} disablePadding>
                <ListItemButton onClick={() => onButtonClick(info.navigationLink)}>
                    <ListItemText primary={info.title} />
                </ListItemButton>
            </ListItem>
            ))}
            </List>
        </Box>
      </MaterialDrawer>
    </div>
  );
}