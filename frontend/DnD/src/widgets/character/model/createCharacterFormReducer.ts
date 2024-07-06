import { ClassIdType } from "@/entities/classes";
import { Race } from "@/entities/races";
import { ExpandedInventoryItem, InventoryItem } from "@/features/inventory";
import { Aligments } from "@/shared/types/domainTypes";
import { FormField } from "@/shared/types/IFormField";
import { isDecimal } from "@/shared/utils/isDecimal";
import { error } from "console";
import { useReducer } from "react";

const fullyUndefined = {
    value: undefined,
    error: null
};

export type Steps = 1 | 2 | 3 | 4 ;

type Step1State = {
    name: FormField<string>;
    isPublic: FormField<boolean>;
    coinsAffectWeight: FormField<boolean>;
};

const setp1Init: Step1State = {
    name: fullyUndefined,
    isPublic: {
        value: true,
        error: null
    },
    coinsAffectWeight: {
        value: false,
        error: null
    }
};

type Step2State = {
    race: FormField<Race>;
    raceTraitsAdjustments: FormField<{
        [name: string]: number
    }>;
    classId: FormField<ClassIdType>;
    skillTraitsMastery: FormField<string[]>;
    classXp: FormField<number>;
    strength: FormField<number>;
    dexterity: FormField<number>;
    constitution: FormField<number>;
    intelligence: FormField<number>;
    wisdom: FormField<number>;
    charisma: FormField<number>;
};

const step2Init: Step2State = {
    race: fullyUndefined,
    raceTraitsAdjustments: fullyUndefined,
    classId: fullyUndefined,
    skillTraitsMastery: fullyUndefined,
    classXp: {
        value: 0,
        error: null,
    },
    strength: {
        value: 15,
        error: null,
    },
    dexterity: {
        value: 14,
        error: null,
    },
    constitution: {
        value: 13,
        error: null,
    },
    intelligence: {
        value: 12,
        error: null,
    },
    wisdom: {
        value: 10,
        error: null,
    },
    charisma: {
        value: 8,
        error: null,
    },
};

type Step3State = {
    base64Image: FormField<string>,
    age: FormField<number>,
    speed: FormField<number>,
    alignment: FormField<Aligments>,
    background: FormField<string>,
    languages: FormField<string[]>,
    flaws: FormField<string[]>,
    bonds: FormField<string[]>,
    otherTraits: FormField<string[]> 
}

const step3Init: Step3State = {
    base64Image: fullyUndefined,
    age: fullyUndefined,
    speed: fullyUndefined,
    alignment: {
        value: Aligments.any,
        error: null
    },
    background: fullyUndefined,
    languages: fullyUndefined,
    flaws: fullyUndefined,
    bonds: fullyUndefined,
    otherTraits: fullyUndefined,
};

type Step4State = {
    inventory: FormField<ExpandedInventoryItem[]>
};

const step4Init: Step4State = {
    inventory: {
        value: [],
        error: null,
    },
};

type CreateCharacterState = Step1State & Step2State & Step3State & Step4State;

export type CreateCharacterFormState = {
    step: Steps;
    formError: string | undefined;
} & CreateCharacterState;

export const initialState: CreateCharacterFormState = {
    step: 1,
    formError: undefined,
    ...setp1Init,
    ...step2Init,
    ...step3Init,
    ...step4Init,
};

export type StateKeys = keyof CreateCharacterState;

export type Action =
  | { type: 'SetStep', step: Steps }
  | { type: 'SetField', fieldName: StateKeys, value: any, error?: string }
  | { type: 'SetFormError', error: string | undefined };


export function reducer(state: CreateCharacterFormState, action: Action): CreateCharacterFormState {
  switch (action.type) {
    case 'SetStep':
        let resetFields;
        if (action.step < state.step) {
            if (action.step < 4) {
                resetFields = step4Init;
            } 
            if (action.step < 3) {
                resetFields = {
                    ...resetFields,
                    ...step3Init,
                };
            }
            if (action.step < 2) {
                resetFields = {
                    ...resetFields,
                    ...step2Init
                };
            }
        }

        return { 
            ...state, 
            ...resetFields,
            step: action.step 
        };
        
    case 'SetField':
        return { 
            ...state, 
            [action.fieldName]: {
                value: action.value,
                error: action.error ?? null
            }
        };
    case 'SetFormError':
        return {
            ...state,
            formError: action.error
        };
    default:
        return state;
  }
}

const carryGetRequireFieldFunction = (dispatch: React.Dispatch<Action>) => function(fieldName: StateKeys, value?: any) {
    return dispatch({
        type: "SetField",
        fieldName,
        value,
        error: "Поле обязательно."
    });
};

const carrySetField = (dispatch: React.Dispatch<Action>) => function(fieldName: StateKeys, value: any, error?: string) {
    return dispatch({
        type: "SetField",
        fieldName,
        value,
        error: error
    });
};

function isValidStep1(state: CreateCharacterFormState, dispatch: React.Dispatch<Action>) {
    let error: boolean = false;
    const requireField = carryGetRequireFieldFunction(dispatch);

    if(!state.name.value){
        error = true;
        requireField("name", "");
    } else {
        const name = state.name.value!.trim();
        if (name.length == 0){
            error = true;
            requireField("name", name);
        }
    }

    return !error;
}

function validateAbility(dispatch: React.Dispatch<Action>, formField: "strength" | "dexterity" | "constitution" | "intelligence" | "wisdom" | "charisma", value: number | undefined) {
    const setField = carrySetField(dispatch);

    if (!value) {
        setField(formField, 3, "Поле обязательно.");
        return false;
    }

    if (value < 3 || value > 18 || isDecimal(value)) {
        setField(formField, 3, "Не валидное значение.");
        return false;
    }

    setField(formField, Math.floor(value));

    return true;
} 


function isValidStep2(state: CreateCharacterFormState, dispatch: React.Dispatch<Action>) {
    let error = false;
    const requireField = carryGetRequireFieldFunction(dispatch);
    const setField = carrySetField(dispatch);

    if (!state.race.value || !state.race.value.id) {
        error = true;
        requireField("race");
        setField("raceTraitsAdjustments", undefined);
    } else if (state.race.error) {
        setField("race", state.race.value);
    }

    if (!state.classId.value || state.classId.value.length == 0) {
        error = true;
        requireField("classId");
        setField("skillTraitsMastery", []);
    } else if (!state.skillTraitsMastery.value || state.skillTraitsMastery.value!.length == 0) {
        error = true;
        requireField("skillTraitsMastery", []);
    } else if (state.classId.error || state.skillTraitsMastery.error) {
        if (state.classId.error) {
            setField("classId", state.classId.value);
        } 
        if (state.skillTraitsMastery.error) {
            setField("skillTraitsMastery", state.skillTraitsMastery.value);
        }
    }

    if(!state.classXp.value) {
        error = true;
        requireField("classXp");
    } else if (state.classXp.value < 0) {
        error = true;
        setField("classXp", 0, "Только положительные числа.");
    } else if (isDecimal(state.classXp.value)) {
        setField("classXp", Math.floor(state.classXp.value));
    } else {
        setField("classXp", state.classXp.value);
    }

    return !error
        && !validateAbility(dispatch, "strength", state.strength.value)
        && !validateAbility(dispatch, "dexterity", state.dexterity.value)
        && !validateAbility(dispatch, "constitution", state.constitution.value)
        && !validateAbility(dispatch, "intelligence", state.intelligence.value)
        && !validateAbility(dispatch, "wisdom", state.wisdom.value)
        && !validateAbility(dispatch, "charisma", state.charisma.value)
}

function isValidStep3(state: CreateCharacterFormState, dispatch: React.Dispatch<Action>) {
    const setField = carrySetField(dispatch);

    setField("background", state.background.value?.trim());
    setField("flaws", state.flaws.value?.map(x => x.trim()));
    setField("bonds", state.bonds.value?.map(x => x.trim()));
    setField("languages", state.languages.value?.map(x => x.trim()));
    setField("otherTraits", state.otherTraits.value?.map(x => x.trim()));

    if (!state.alignment.value || state.alignment.value == Aligments.any) {
        setField("alignment", Aligments.any, "Поле обязательно.");
        return false;
    }

    return true;
}

function isValidStep4(state: CreateCharacterFormState, _: React.Dispatch<Action>) {

    return state.inventory?.value != undefined && state.inventory!.value.every(x => x.count >= 1);
}


export function useCreateCharacterReducer() {
    const [state, dispatch] = useReducer(reducer, initialState);

    return {
        state,
        setField: carrySetField(dispatch),
        setStep: (step: Steps) =>
            dispatch({
                type: "SetStep",
                step
            }),
        setFormError: (error?: string) => dispatch({
            type: 'SetFormError',
            error
        }),
        isValidStep1: () => isValidStep1(state, dispatch),
        isValidStep2: () => isValidStep2(state, dispatch),
        isValidStep3: () => isValidStep3(state, dispatch),
        isValidStep4: () => isValidStep4(state, dispatch),
    }
}