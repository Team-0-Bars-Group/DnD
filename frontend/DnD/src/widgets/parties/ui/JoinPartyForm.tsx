import { useJoinPartyMutation } from "@/features/party";
import { Box, Button, Stack, TextField, Typography } from "@mui/material";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import SelectCharacterDialog from "./SelectCharacterDialog";

export default function JoinPartyForm() {
    const navigate = useNavigate();
    const [partyId, setPartyId] = useState<string | undefined>();
    const [characterId, setCharacterId] = useState<string | undefined>();
    const [partyError, setPartyError] = useState<string>();
    const [accessCode, setAccessCode] = useState<string | undefined>();
    const [accessCodeError, setAccessCodeError] = useState<string>();
    const [requestError, setRequestError] = useState<string>();

    const [showChracterList, setShowCharacterList] = useState(false);
    const ref = useRef<HTMLFormElement>(null);

    const [joinParty, { isLoading, isSuccess, data, isError, error }] = useJoinPartyMutation();

    const onCloseCharacterList = (characterId: string | undefined) => {
        setCharacterId(characterId);
        setShowCharacterList(false);
        if (characterId) {
            ref.current?.submit();
        }
    };

    async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const empty = "";

        if (isLoading) {
            return;
        }

        let validForm = true;
        if (!partyId) {
            setPartyError("Поле обязательно.");
            validForm = false;
        }
        if (!accessCode) {
            setAccessCodeError("Поле обязательно.");
            validForm = false;
        }

        setRequestError(empty);
        if (!validForm) {
            return;
        } else {
            setAccessCodeError(empty);
            setPartyError(empty)
        }

        await joinParty({ partyId: partyId!, accessCode: accessCode!, characterId: characterId!});
    }

    useEffect(() => {
        if (isLoading) {
            return;
        }

        if (isSuccess) {
            navigate(`/game/${data.joinParty.userPartyDto?.id ?? partyId}`);
            return;
        } 

        if (isError) {
            setRequestError("Ошибка при запросе.");
        }

    }, [isSuccess, error, isLoading, data]);

    return  <Box ref={ref} component="form" noValidate onSubmit={handleSubmit} sx={{ mt: 1 }}>
        <TextField
            value={partyId}
            onChange={(e) => setPartyId(e.target.value.trim())}
            helperText={partyError}
            error={partyError != undefined}
            margin="dense"
            required
            fullWidth
            label="Индетификатор отряда"
        />
        <TextField
            value={accessCode}
            onChange={(e) => setAccessCode(e.target.value.trim())}
            helperText={accessCodeError}
            error={accessCodeError != undefined}
            margin="dense"
            required
            fullWidth
            label="Код доступа"
        />
        <Stack alignItems="center" marginTop={!requestError ? 3 : 0}>
            { requestError && 
                <Typography component="span" color="error" marginBottom={3}>
                    {requestError}
                </Typography>
            }
            <Button variant="contained" onClick={() => setShowCharacterList(true)}  size="large" fullWidth disabled={isLoading}>
                Присоединиться
            </Button>
        </Stack>
        <SelectCharacterDialog onClose={onCloseCharacterList} open={showChracterList} />
    </Box>
}