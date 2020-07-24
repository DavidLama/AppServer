import React, { useState } from "react";
import { Box, Text, Icons } from "asc-web-components";
import RecoverAccessModalDialog from "./recover-access-modal-dialog";
import styled from "styled-components";
import PropTypes from 'prop-types';

const RecoverWrapper = styled(Box)`
    margin: 0 16.7% 0 0;

  @media(max-width: 768px) {
    margin: 0 18.8% 0 0;
}
  @media(max-width: 450px) {
    margin: 0 8.5% 0 0;
}
`;

const RecoverContainer = styled(Box)`
 cursor: pointer;
  .recover-icon {
   @media(max-width: 620px) {
    padding: 16px;
   }
}
  .recover-text {
   @media(max-width: 620px) {
    display: none;
   }
}
`;

const RecoverAccess = ({ t }) => {

    const [visible, setVisible] = useState(false);

    const onRecoverClick = () => {
        setVisible(true);
    };
    const onRecoverModalClose = () => {
        setVisible(false);
    };

    return (
        <>
            <RecoverWrapper
                widthProp="100%"
                heightProp="100%"
                displayProp="flex"
                justifyContent="flex-end"
                alignItems="center">
                <RecoverContainer
                    backgroundProp="#27537F"
                    heightProp="100%"
                    displayProp="flex"
                    onClick={onRecoverClick}>
                    <Box paddingProp="16px 8px 16px 16px" className="recover-icon">
                        <Icons.UnionIcon />
                    </Box>
                    <Box paddingProp="18px 16px 18px 0px" className="recover-text" widthProp="100%">
                        <Text color="#fff" isBold={true}>
                            {t("RecoverAccess")}
                        </Text>
                    </Box>
                </RecoverContainer>
            </RecoverWrapper>
            {visible && <RecoverAccessModalDialog
                visible={visible}
                onRecoverModalClose={onRecoverModalClose}
                t={t}
            />
            }
        </>
    )
}

RecoverAccess.propTypes = {
    t: PropTypes.func.isRequired
};

export default RecoverAccess;
